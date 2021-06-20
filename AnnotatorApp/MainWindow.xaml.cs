using System.ComponentModel;
using System.Windows;
using ComponentLibrary;

namespace AnnotatorApp
{
    public partial class MainWindow
    {
        private readonly MainWindowComponent _mainWindowComponent;
        public MainWindow()
        {
            using (_mainWindowComponent = new MainWindowComponent())
            {
                InitializeComponent();
                ParentGrid.Children.Add(_mainWindowComponent);
            }
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (_mainWindowComponent.ModelDocument == null || !_mainWindowComponent.IsChanged) return;

            var dlg = MessageBox.Show("Имеются несохраненные изменения. Хотите сохранить их?",
                "Предупреждение", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            switch (dlg)
            {
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
                case MessageBoxResult.Yes:
                    _mainWindowComponent.ModelDocument.Save();
                    _mainWindowComponent.Dispose();
                    break;
            }
        }
    }
}
