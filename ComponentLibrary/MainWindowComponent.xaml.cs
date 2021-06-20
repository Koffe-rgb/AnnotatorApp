using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ComponentLibrary.Model;
using ComponentLibrary.Tools;
using Kumo;
using Microsoft.Win32;

namespace ComponentLibrary
{
    public partial class MainWindowComponent : IDisposable
    {
        private string _filepath;
        public bool IsChanged { get; private set; }

        private FlowDocument _flowDocument;
        private readonly UserStyleService _styleService;
        private BookmarkService _bookmarkService;
        private FlowConverter _flowConverter;
        public Document ModelDocument { get; private set; }

        
        private Bookmark _selectedBookmark;
        public ObservableCollection<Bookmark> Bookmarks { get; }
        public ObservableCollection<Node> BmsGroupedByStyle { get; }

        public MainWindowComponent()
        {
            Bookmarks = new ObservableCollection<Bookmark>();
            BmsGroupedByStyle = new ObservableCollection<Node>();

            InitializeComponent();

            _styleService = new UserStyleService();
            _styleService.LoadStyles(UserStyleService.FilePath);
        }

        /// <summary>
        /// Метод загружает документ и настраивает сервисы
        /// </summary>
        /// <returns>Флаг успешной загрузки</returns>
        private bool LoadDocument()
        {
            ModelDocument?.Dispose();

            try
            {
                using (_flowConverter = new FlowConverter(_filepath))
                {
                    _flowDocument = _flowConverter.Convert();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось загрузить файл", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            IsChanged = false;

            ModelDocument = Document.Open(_filepath, true);
            _bookmarkService = new BookmarkService(DocumentViewer, _styleService, ModelDocument);

            return true;
        }


        /// <summary>
        /// Метод создает пустую аннотацию
        /// </summary>
        /// <returns>Пустая аннотация</returns>
        private Bookmark CreateBookmark()
        {
            if (DocumentViewer.Selection == null || DocumentViewer.Selection.IsEmpty)
            {
                MessageBox.Show("Выделите область", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var selectionStart = DocumentViewer.Selection.Start;
            var selectionEnd = DocumentViewer.Selection.End;
            var text = new TextRange(selectionStart, selectionEnd).Text;

            // не учитываем последний символ, если он - пробел
            if (char.IsWhiteSpace(text[^1]))
                selectionEnd = selectionEnd.GetNextInsertionPosition(LogicalDirection.Backward);

            // находим индексы выделенной области
            var idxStart = BookmarkService.FindOffsetOfTextPointers(DocumentViewer.Document.ContentStart, selectionStart);
            var idxEnd = idxStart + BookmarkService.FindOffsetOfTextPointers(selectionStart, selectionEnd);

            // пустая аннотация
            return new Bookmark(idxStart, idxEnd, "", "", text);
        }

        /// <summary>
        /// Обработчик события создания аннотации
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddAnnotation_Click(object sender, RoutedEventArgs e)
        {
            var bookmark = CreateBookmark();

            // вызываем окно настройки аннотации
            var annotationWindow = new AnnotationWindow(Mode.ToCreate, bookmark);
            if (annotationWindow.ShowDialog() != true)
                return;

            // получаем настроенную аннотацию
            bookmark = annotationWindow.Bookmark;
            // и вставляем ее в OOXML док
            try { _bookmarkService.InsertBookmark(bookmark); }
            catch (Exception)
            {
                MessageBox.Show("Непредвиденная ошибка. Сожалеем об этом.",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // локальные копии:

            Bookmarks.Add(bookmark);

            if (BmsGroupedByStyle.Count(node => node.StyleName == bookmark.Type) == 0)
            {
                var node = new Node(bookmark.Type);
                node.StyledBookmarks.Add(bookmark);
                BmsGroupedByStyle.Add(node);
            }
            else
            {
                BmsGroupedByStyle.First(node => node.StyleName == bookmark.Type)
                    .StyledBookmarks
                    .Add(bookmark);
            }

            IsChanged = true;
        }

        /// <summary>
        /// Обработчки события удаления аннотации
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteAnnotation_Click(object sender, RoutedEventArgs e)
        {
            var bookmark = _selectedBookmark;
            if (bookmark == null)
                return;

            // удаляем локальные копии
            Bookmarks.Remove(bookmark);

            var styledBookmarks = BmsGroupedByStyle
                .First(node => node.StyleName == bookmark.Type)
                .StyledBookmarks;
            var bmToDelete = styledBookmarks
                .First(bm =>
                    bm.Literal == bookmark.Literal &&
                    bm.Text == bookmark.Text);
            styledBookmarks.Remove(bmToDelete);

            // удаляем из OOXML дока
            try { _bookmarkService.DeleteBookmark(bookmark); }
            catch (Exception)
            {
                MessageBox.Show("Не удалось удалить аннотацию", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            TextBox.Text = string.Empty;
            
            IsChanged = true;
        }

        /// <summary>
        /// Обработчик события изменения аннотации
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeAnnotation_Click(object sender, RoutedEventArgs e)
        {
            var oldBookmark = _selectedBookmark;
            if (oldBookmark == null)
                return;

            // вызов окна настройки
            var annotationWindow = new AnnotationWindow(Mode.ToUpdate, oldBookmark.Clone());
            if (annotationWindow.ShowDialog() != true) 
                return;

            var newBookmark = annotationWindow.Bookmark;

            // изменяем в OOXML доке
            try { _bookmarkService.UpdateBookmark(oldBookmark, newBookmark); }
            catch (Exception)
            {
                MessageBox.Show("Не удалось заменить аннотацию", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // заменяем локальные копии
            var idx = Bookmarks.IndexOf(oldBookmark);
            Bookmarks.Remove(oldBookmark);
            Bookmarks.Insert(idx, newBookmark);

            var styledBookmarks = BmsGroupedByStyle
                .First(node => node.StyleName == oldBookmark.Type)
                .StyledBookmarks;
            var bmToDelete = styledBookmarks
                .First(bm =>
                    bm.Literal == oldBookmark.Literal &&
                    bm.Text == oldBookmark.Text);
            styledBookmarks.Remove(bmToDelete);

            if (BmsGroupedByStyle.Count(node => node.StyleName == newBookmark.Type) == 0)
            {
                var node = new Node(newBookmark.Type);
                node.StyledBookmarks.Add(newBookmark);
                BmsGroupedByStyle.Add(node);
            }
            else
            {
                BmsGroupedByStyle.First(node => node.StyleName == newBookmark.Type)
                    .StyledBookmarks
                    .Add(newBookmark);
            }

            IsChanged = true;
        }

        private void LvStackAnnotations_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LvSelectionChanged(sender);
        }

        private void MiSave_Click(object sender, RoutedEventArgs e)
        {
            ModelDocument?.Save();
            ModelDocument?.Dispose();
            ModelDocument = Document.Open(_filepath, true);
            IsChanged = false;
            MessageBox.Show("Сохранено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MiOpenFileDialog_Click(object sender, RoutedEventArgs e)
        {
            if (ModelDocument != null && IsChanged)
            {
                var dlg = MessageBox.Show("Имеются несохраненные изменения. Хотите сохранить их?",
                    "Предупреждение", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                switch (dlg)
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.Yes:
                        ModelDocument.Save();
                        ModelDocument.Dispose();
                        break;
                }
            }

            var openFileDialog = new OpenFileDialog
            {
                DefaultExt = ".docx",
                Filter = "Документ MS Word |*.docx"
            };


            if (openFileDialog.ShowDialog() != true)
                return;

            _filepath = openFileDialog.FileName;

            var isLoaded = LoadDocument();

            if (!isLoaded)
                return;

            DocumentViewer.Document = _flowDocument;

            _bookmarkService.InsertAtOpening();

            Bookmarks.Clear();
            foreach (var bookmark in _bookmarkService.Bookmarks)
            {
                Bookmarks.Add(bookmark);
            }

            BmsGroupedByStyle.Clear();
            foreach (var type in Bookmarks.Select(bookmark => bookmark.Type).Distinct())
            {
                var node = new Node(type);
                foreach (var bookmark in Bookmarks.Where(bm => bm.Type == type))
                {
                    node.StyledBookmarks.Add(bookmark);
                }
                BmsGroupedByStyle.Add(node);
            }
        }

        private void MiLoadStylesFile_Click(object sender, RoutedEventArgs e)
        {
            if (_styleService.LoadStyles())
                _bookmarkService?.UpdateStyles();
        }

        private void LvAnnotations_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LvSelectionChanged(sender);
        }

        private void LvSelectionChanged(object sender)
        {
            _selectedBookmark = (Bookmark)((ListView)sender).SelectedItem;
            if (_selectedBookmark == null)
                return;
            TextBox.Text = _selectedBookmark.Literal;
        }

        private void ExpStackAnnotations_OnExpanded(object sender, RoutedEventArgs e)
        {
            ExpanderRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void ExpStackAnnotations_OnCollapsed(object sender, RoutedEventArgs e)
        {
            ExpanderRow1.Height = new GridLength(1, GridUnitType.Auto);
        }

        private void ExpGroupAnnotations_OnExpanded(object sender, RoutedEventArgs e)
        {
            ExpanderRow2.Height = new GridLength(1, GridUnitType.Star);
        }

        private void ExpGroupAnnotations_OnCollapsed(object sender, RoutedEventArgs e)
        {
            ExpanderRow2.Height = new GridLength(1, GridUnitType.Auto);
        }

        private void TreeViewStyles_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            switch (TreeViewStyles.SelectedItem)
            {
                case Node _:
                    TextBox.Text = string.Empty;
                    _selectedBookmark = null;
                    break;
                case Bookmark bm:
                    _selectedBookmark = bm;
                    if (_selectedBookmark == null)
                        return;
                    TextBox.Text = _selectedBookmark.Literal;
                    break;
            }
        }

        private void MiOpenStyleEditor_Click(object sender, RoutedEventArgs e)
        {
            var editorWindow = new StyleEditor(UserStyleService.StylesDictionary);
            if (editorWindow.ShowDialog() == true)
            {
                _bookmarkService?.UpdateStyles();
            }
        }

        public void Dispose()
        {
            ModelDocument?.Dispose();
        }
    }
}
