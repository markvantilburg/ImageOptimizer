using System.Collections.Generic;
using System.Linq;


namespace MadsKristensen.ImageOptimizer
{
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdOptimizelossy)]
    internal class OptimizeLossyCommand : BaseCommand<OptimizeLossyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> images = await OptimizeLosslessCommand.GetImageFilesAsync(e);

            if (!images.Any())
            {
                await VS.StatusBar.ShowMessageAsync("No images found to optimize");
            }
            else
            {
                Solution solution = await VS.Solutions.GetCurrentSolutionAsync();
                Optimizer optimizer = new();
                optimizer.OptimizeAsync(images, CompressionType.Lossy, solution.FullPath).FireAndForget();
            }
        }
    }
}
