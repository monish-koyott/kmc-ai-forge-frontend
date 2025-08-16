using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using KmcAiBlazorApp.Models;

namespace KmcAiBlazorApp.Services
{
    public class SignalRNotificationService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly ILogger<SignalRNotificationService> _logger;
        private readonly string _hubUrl;

        public event Action<ProcessingUpdate>? OnProcessingUpdate;
        public event Action<DocumentValidationUpdate>? OnDocumentValidationUpdate;
        public event Action<PortfolioCompletionUpdate>? OnPortfolioCompletionUpdate;
        public event Action<CompanyHouseValidationUpdate>? OnCompanyHouseValidationUpdate;
        public event Action<ProcessingCompleteUpdate>? OnProcessingCompleteUpdate;
        public event Action<string>? OnConnectionStatusChanged;

        public SignalRNotificationService(ILogger<SignalRNotificationService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _hubUrl = configuration["SignalR:DocumentProcessingHubUrl"] ?? $"{configuration["Urls:Backend"]}/documentProcessingHub";
            _logger.LogInformation("SignalR Hub URL: {HubUrl}", _hubUrl);
        }

        public async Task StartConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Starting SignalR connection to: {HubUrl}", _hubUrl);
                
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                // Register event handlers - match your API method names
                _hubConnection.On<ProcessingUpdate>("ProcessingUpdate", HandleProcessingUpdate);
                _hubConnection.On<DocumentValidationUpdate>("DocumentValidationUpdate", HandleDocumentValidationUpdate);
                _hubConnection.On<PortfolioCompletionUpdate>("PortfolioCompletionUpdate", HandlePortfolioCompletionUpdate);
                _hubConnection.On<CompanyHouseValidationUpdate>("CompanyHouseValidationUpdate", HandleCompanyHouseValidationUpdate);
                _hubConnection.On<ProcessingCompleteUpdate>("ProcessingCompleteUpdate", HandleProcessingCompleteUpdate);

                // Connection event handlers
                _hubConnection.Reconnecting += OnReconnecting;
                _hubConnection.Reconnected += OnReconnected;
                _hubConnection.Closed += OnClosed;

                await _hubConnection.StartAsync();
                OnConnectionStatusChanged?.Invoke("Connected");
                _logger.LogInformation("SignalR connection established");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish SignalR connection");
                OnConnectionStatusChanged?.Invoke("Failed to connect");
            }
        }

        public async Task JoinPortfolioGroupAsync(string portfolioId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                // Your API's JoinPortfolioGroup method expects just the portfolioId
                // It will internally create the group name as "portfolio_{portfolioId}"
                await _hubConnection.InvokeAsync("JoinPortfolioGroup", portfolioId);
                _logger.LogInformation("Joined portfolio group: {PortfolioId}", portfolioId);
            }
            else
            {
                _logger.LogWarning("Cannot join group: SignalR connection not established");
            }
        }

        public async Task LeavePortfolioGroupAsync(string portfolioId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                // Your API's LeavePortfolioGroup method expects just the portfolioId
                // It will internally remove from group "portfolio_{portfolioId}"
                await _hubConnection.InvokeAsync("LeavePortfolioGroup", portfolioId);
                _logger.LogInformation("Left portfolio group: {PortfolioId}", portfolioId);
            }
            else
            {
                _logger.LogWarning("Cannot leave group: SignalR connection not established");
            }
        }



        private void HandleProcessingUpdate(ProcessingUpdate update)
        {
            _logger.LogInformation("Received processing update for portfolio {PortfolioId}: {Status}", 
                update.PortfolioId, update.Status);
            OnProcessingUpdate?.Invoke(update);
        }
  
        private void HandleDocumentValidationUpdate(DocumentValidationUpdate update)
        {
            _logger.LogInformation("Received document validation update for portfolio {PortfolioId}", 
                update.PortfolioId);
            OnDocumentValidationUpdate?.Invoke(update);
        }

        private void HandlePortfolioCompletionUpdate(PortfolioCompletionUpdate update)
        {
            _logger.LogInformation("Received portfolio completion update for portfolio {PortfolioId}", 
                update.PortfolioId);
            OnPortfolioCompletionUpdate?.Invoke(update);
        }

        private void HandleCompanyHouseValidationUpdate(CompanyHouseValidationUpdate update)
        {
            _logger.LogInformation("Received company house validation update for portfolio {PortfolioId}", 
                update.PortfolioId);
            OnCompanyHouseValidationUpdate?.Invoke(update);
        }

        private void HandleProcessingCompleteUpdate(ProcessingCompleteUpdate update)
        {
            _logger.LogInformation("Received processing complete update for portfolio {PortfolioId}", 
                update.PortfolioId);
            OnProcessingCompleteUpdate?.Invoke(update);
        }

        private Task OnReconnecting(Exception? exception)
        {
            _logger.LogWarning(exception, "SignalR connection lost, attempting to reconnect...");
            OnConnectionStatusChanged?.Invoke("Reconnecting");
            return Task.CompletedTask;
        }

        private Task OnReconnected(string? connectionId)
        {
            _logger.LogInformation("SignalR connection reestablished. ConnectionId: {ConnectionId}", connectionId);
            OnConnectionStatusChanged?.Invoke("Reconnected");
            return Task.CompletedTask;
        }

        private Task OnClosed(Exception? exception)
        {
            _logger.LogWarning(exception, "SignalR connection closed");
            OnConnectionStatusChanged?.Invoke("Disconnected");
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }
}