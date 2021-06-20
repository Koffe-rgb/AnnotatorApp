using ComponentLibrary.Model;
using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ComponentLibrary.Tools
{
  /// <summary>
  /// Класс занимается управлением коллекциями стилей,
  /// а также применяет к аннотациям в тексте потокового документа
  /// стили
  /// </summary>
  public class UserStyleService
  {
    public static Style DefaultStyle { get; set; }
    public static ResourceDictionary StylesDictionary { get; private set; }

    public static string FilePath { get; private set; } = @"./UserStyles.xaml";

    public UserStyleService()
    {
      DefaultStyle = new Style
      {
        TargetType = typeof(Run),
        Setters = 
        {
          new Setter 
          {
            Property = TextElement.ForegroundProperty,
            Value = Brushes.Black
          },
          new Setter
          {
              Property = TextElement.BackgroundProperty,
              Value = Brushes.White
          },
          new Setter
          {
              Property = TextElement.FontFamilyProperty,
              Value = new FontFamily("Times New Roman")
          },
          new Setter
          {
              Property = TextElement.FontStretchProperty,
              Value = FontStretches.Normal
          },
          new Setter
          {
              Property = TextElement.FontStyleProperty,
              Value = FontStyles.Normal
          },
          new Setter
          {
              Property = TextElement.FontWeightProperty,
              Value = FontWeights.Normal
          },
          new Setter
          {
              Property = TextElement.FontSizeProperty,
              Value = 16.0
          }
        }
      };
    }

    /// <summary>
    /// Метод загружает в компонент стили из указанного файла стилей
    /// через стандартный диалог выбора файла
    /// </summary>
    /// <returns>Флаг загрузки</returns>
    public bool LoadStyles()
    {
      var dialog = new OpenFileDialog
      {
        DefaultExt = ".xaml",
        Filter = "Файлы стилей |*.xaml"
      };

      if (dialog.ShowDialog() != true)
        return false;

      FilePath = dialog.FileName;

      LoadStyles(FilePath);
      return true;
    }

    /// <summary>
    /// Метод загружает в компонент стили из указанного файла стилей
    /// </summary>
    /// <param name="path">Путь до файла стилей</param>
    public void LoadStyles(string path)
    {
      try
      {
        StylesDictionary = new ResourceDictionary { Source = new Uri(path, UriKind.RelativeOrAbsolute) };
      }
      catch (Exception)
      {
        MessageBox.Show("Не удалось загрузить файл стилей. Возможно, в этом файле не содержатся стили.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    /// <summary>
    /// Метод применяет к указанной аннотации указанный стиль.
    /// Если стиль не был найден или не был указан, к аннотации
    /// применяется стандартный стиль
    /// </summary>
    /// <param name="bookmark">Объект аннотации</param>
    /// <param name="run">Прогон текста в потоковом документе</param>
    /// <param name="styleName">Название стиля</param>
    /// <returns>Флаг, указывающий был ли применен стандартный стиль</returns>
    public bool ApplyStyle(Bookmark bookmark, Run run, string styleName = "default")
    {
      bookmark.Type = styleName;

      run.Style = styleName != "default" ?
          (Style)StylesDictionary[styleName] ?? DefaultStyle :
          DefaultStyle;

      return run.Style != DefaultStyle;
    }
  }
}