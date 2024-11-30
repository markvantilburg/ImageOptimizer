using System.IO;
using System.Windows;
using EnvDTE;

namespace MadsKristensen.ImageOptimizer
{
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdCopyDataUri)]
    public class CopyBase64Command : BaseCommand<CopyBase64Command>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            DTE dte = await VS.GetRequiredServiceAsync<DTE, DTE>();

            var copyPath = dte.SelectedItems.Item(1).ProjectItem?.FileNames[0];
            var base64 = Base64Helpers.CreateBase64ImageString(copyPath);

            Clipboard.SetText(base64);

            await VS.StatusBar.ShowMessageAsync("DataURI copied to clipboard (" + base64.Length + " characters)");
        }
    }

    public static class Base64Helpers
    {
        public static string CreateBase64ImageString(string imageFile)
        {
            return "data:"
                        + GetMimeTypeFromFileExtension(imageFile)
                        + ";base64,"
                        + Convert.ToBase64String(File.ReadAllBytes(imageFile));
        }

        private static string GetMimeTypeFromFileExtension(string file)
        {
            var ext = Path.GetExtension(file).TrimStart('.');

            return ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "svg" => "image/svg+xml",
                _ => "image/" + ext,
            };
        }
    }
}
