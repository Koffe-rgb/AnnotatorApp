using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ComponentLibrary.Model;
using ComponentLibrary.Tools;

namespace ComponentLibrary
{
    public enum Mode
    {
        ToCreate,
        ToUpdate
    }

    public partial class AnnotationWindow
    {
        public Bookmark Bookmark { get; }

        public AnnotationWindow(Mode mode, Bookmark bookmark)
        {
            InitializeComponent();

            Bookmark = bookmark;

            TxtBoxAnnotatedText.Text = bookmark.Text;

            CbxStyles.ItemsSource = UserStyleService.StylesDictionary;
            CbxStyles.SelectedIndex = 0;

            if (mode == Mode.ToCreate)
            {
                Title = "Создание аннотации";
            }
            else
            {
                Title = "Изменение аннотации";
                TxtBoxAnnotationText.Text = bookmark.Literal;

                CbxStyles.SelectedIndex = UserStyleService.StylesDictionary
                    .Keys
                    .Cast<string>()
                    .TakeWhile(key => !string.Equals(key, bookmark.Type, StringComparison.Ordinal))
                    .Count();
            }
        }

        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Bookmark.Type = ((DictionaryEntry) CbxStyles.SelectedItem).Key.ToString();
            Bookmark.Literal = TxtBoxAnnotationText.Text.Trim();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void CbxStyles_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var name = ((DictionaryEntry)CbxStyles.SelectedItem).Key.ToString();
            StyledRun.Style = (Style) UserStyleService.StylesDictionary[name];
        }
    }
}
