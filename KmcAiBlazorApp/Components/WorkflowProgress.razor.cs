using Microsoft.AspNetCore.Components;

namespace KmcAiBlazorApp.Components;

public partial class WorkflowProgress : ComponentBase
{
    [Parameter]
    public int CurrentStep { get; set; } = 1;
    
    [Parameter]
    public EventCallback<int> CurrentStepChanged { get; set; }
    
    private async Task SetActiveStep(int step)
    {
        if (CurrentStep != step)
        {
            CurrentStep = step;
            await CurrentStepChanged.InvokeAsync(step);
        }
    }
}
