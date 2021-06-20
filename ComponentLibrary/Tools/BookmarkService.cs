using ComponentLibrary.Model;
using Kumo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ComponentLibrary.Tools
{
  /// <summary>
  /// Класс-оболочка над интерфейсом доступа к OOXML документу. Управляет аннотациями в исходном документе и потоковом.
  /// </summary>
  public class BookmarkService
  {
    private readonly FlowDocumentScrollViewer _flowDocumentContainer;
    private readonly UserStyleService _styleService;
    private readonly Document _document;
    private readonly Dictionary<Bookmark, Run> _highlightsTable;

    public ObservableCollection<Bookmark> Bookmarks;

    /// <summary>
    /// Класс-оболочка над интерфейсом доступа к OOXML документу. Управляет коллекциями аннотаций в исходном документе и потоковом.
    /// </summary>
    /// <param name="flowDocumentContainer">Контейнер потокового документа</param>
    /// <param name="styleService">Сервис работы со стилями в потоком документе</param>
    /// <param name="document">Kumo Document</param>
    public BookmarkService(FlowDocumentScrollViewer flowDocumentContainer, UserStyleService styleService, Document document)
    {
      _flowDocumentContainer = flowDocumentContainer;
      _styleService = styleService;
      _document = document;
      _highlightsTable = new Dictionary<Bookmark, Run>();

      Bookmarks = new ObservableCollection<Bookmark>();
    }

    /// <summary>
    /// Простой метод для создания URI с предопределенными параметрами
    /// </summary>
    /// <param name="value">Последнее значение компонента Path у URI</param>
    /// <returns>URI с предопределенными параметрами</returns>
    private static Uri MakeUri(string value) => new($"https://kumo.org/types/{value}");

    /// <summary>
    /// Простой метод для поиска в исходном документе свойства, которое моделируется Bookmark
    /// </summary>
    /// <param name="bookmark">Bookmark, который моделирует свойство в исходном документе</param>
    /// <returns>Свойство Property</returns>
    private static Property FindMatchingProperty(Bookmark bookmark) => bookmark.Range.Properties.First(prop =>
        prop.Name == MakeUri(bookmark.Type) && prop.Value == new Resource.Literal(bookmark.Literal));

    /// <summary>
    /// Метод обратный FindTextPointerFromOffset. Находит отступ от заданного TextPointer searchFrom
    /// до искомого TextPointer targertPosition. В отступ <b>НЕ</b> входят символы перевода строки.
    /// </summary>
    /// <param name="searchFrom">TextPointer начала поиска</param>
    /// <param name="targetPosition">TextPointer искомой позиции</param>
    /// <returns>Отступ от изначального TextPointer до искомого</returns>
    public static int FindOffsetOfTextPointers(TextPointer searchFrom, TextPointer targetPosition)
    {
      var counter = 0;
      var next = searchFrom;

      // пока не кончился файл или не дошли до нужного textpointer
      while (next.CompareTo(targetPosition) != 0)
      {
        var prev = next;
        next = next.GetNextInsertionPosition(LogicalDirection.Forward);

        if (next == null)
          break;

        var text = new TextRange(prev, next).Text.Trim();
        if (text.Equals("\r\n")) // для ситуаций с linebreaker'ом
          continue;

        // если указывал на текст - то увеличиваем счетчик
        if (next.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.Text)
          counter++;
      }

      return counter;
    }

    /// <summary>
    /// Метод обратный FindOffsetOfTextPointer. Находит в потоковом документе TextPointer,
    /// основываясь на отступе от заданного TextPointer. Отступ основан на <b>видимых</b>
    /// символах потокового документа.
    /// </summary>
    /// <param name="searchFrom">TextPointer начала поиска</param>
    /// <param name="offset">Отступ в <b>видимых</b> символах потокового документа</param>
    /// <returns>TextPointer находящийся на заданном отступе, от изначального TextPointer</returns>
    public TextPointer FindTextPointerFromOffset(TextPointer searchFrom, int offset)
    {
      if (offset == 0)
        return searchFrom;

      var next = searchFrom;
      var counter = 0;

      // пока не кончился файл или не дошли до нужного оффсета
      while (next != null && counter <= offset)
      {
        // при достижении конца документа возвращаем EOF = null
        if (next.CompareTo(_flowDocumentContainer.Document.ContentEnd) == 0)
          return null;

        // если текущий textpointer указывает на текст документа, увеличиваем счетчик, идем дальше
        if (next.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
        {
          next = next.GetNextInsertionPosition(LogicalDirection.Forward);
          counter++;
        }
        else
        {
          // иначе только идем дальше
          next = next.GetNextContextPosition(LogicalDirection.Forward);
        }
      }

      return next;
    }

    /// <summary>
    /// Метод для вставки в потоковый документ аннотации 
    /// </summary>
    /// <param name="selectedRange">Аннотируемый текст</param>
    /// <param name="bookmark">Аннотация</param>
    /// <param name="startPos">Позиция начала аннотируемого текста в потоковом документе</param>
    private void InsertToFlowDoc(TextRange selectedRange, Bookmark bookmark, TextPointer startPos)
    {
      // стираем текст, если последний символ пробел, оставляем его
      selectedRange.Text = char.IsWhiteSpace(selectedRange.Text[^1]) ? " " : string.Empty;

      var annotated = new Run(bookmark.Range.Text(), startPos)
      {
        ToolTip = new ToolTip // отображение аннотации
        {
          Background = Brushes.LightYellow,
          Content = new Label { Content = bookmark.Literal, FontStyle = FontStyles.Italic }
        }
      };

      // локально сохраняем для работы в этой же сессии
      if (!_highlightsTable.ContainsKey(bookmark))
        _highlightsTable.Add(bookmark, annotated);

      // оформление стилем
      _styleService.ApplyStyle(bookmark, annotated, bookmark.Type);
    }

    /// <summary>
    /// Метод для вставки в OOXML документ новых аннотаций
    /// </summary>
    /// <param name="bookmark">Аннотация для вставки</param>
    public void InsertBookmark(Bookmark bookmark)
    {
      // вставка в исходный документ
      bookmark.Range = _document.Range(bookmark.Start, bookmark.End);
      bookmark.Range.Attach(new Property(
              MakeUri(bookmark.Type),
              new Resource.Literal(bookmark.Literal)
          )
      );

      // вставка в потоковый документ
      var startPos = _flowDocumentContainer.Selection.Start;
      var endPos = _flowDocumentContainer.Selection.End;
      var selectedRange = new TextRange(startPos, endPos);

      InsertToFlowDoc(selectedRange, bookmark, startPos);
    }

    /// <summary>
    /// Метод для вставки уже существующих аннотаций в потоковый документ при открытии OOXML документа
    /// </summary>
    public void InsertAtOpening()
    {
      // получаем аннотации OOXML документа в исходном виде
      var stars = _document.Stars();
      foreach (var star in stars)
      {
        // для каждого свойства каждой аннотации
        var properties = star.Properties;
        foreach (var property in properties)
        {
          // разбираем на параметры
          var type = property.Name.PathAndQuery.Split("/").Last();
          var literal = ((Resource.Literal)property.Value).Value;
          var start = star.Start;
          var end = star.End;
          var range = _document.Range(start, end);

          // создаем параметры для вставки в потоковый документ
          var bookmark = new Bookmark(start, end, type, literal, range);
          var startPos = FindTextPointerFromOffset(_flowDocumentContainer.Document.ContentStart, start);
          var wordRange = WordBreaker.GetWordRange(startPos);
          startPos = wordRange.Start;
          var endPos = startPos.GetPositionAtOffset(bookmark.Text.Length);

          var textRange = new TextRange(startPos, endPos);

          if (endPos != null && !textRange.Text.Contains(bookmark.Text))
          {
            endPos = endPos.GetNextInsertionPosition(LogicalDirection.Forward);
            textRange = new TextRange(startPos, endPos);
          }

          var selectedRange = textRange;

          Bookmarks.Add(bookmark);

          // вставляем в потоковый документ
          InsertToFlowDoc(selectedRange, bookmark, startPos);
        }
      }
    }

    /// <summary>
    /// Метод для замены в OOXML документ аннотаций
    /// </summary>
    /// <param name="oldBookmark">Старая аннотация</param>
    /// <param name="newBookmark">Новая аннотация</param>
    public void UpdateBookmark(Bookmark oldBookmark, Bookmark newBookmark)
    {
      if (!_highlightsTable.ContainsKey(oldBookmark))
        return;

      // обновляем в исходном документе
      // отсоедияем старое свойство
      var oldProperty = FindMatchingProperty(oldBookmark);
      oldBookmark.Range.Detach(oldProperty);

      // присоеднияем новое
      newBookmark.Range ??= oldBookmark.Range;
      newBookmark.Range.Attach(new Property(
              MakeUri(newBookmark.Type),
              new Resource.Literal(newBookmark.Literal)
          )
      );

      // обновляем в потоковом документе
      var toUpdate = _highlightsTable[oldBookmark];
      toUpdate.ToolTip = new ToolTip
      {
        Background = Brushes.LightYellow,
        Content = new Label { Content = newBookmark.Literal, FontStyle = FontStyles.Italic }
      };

      // обновляем локальную копию
      _highlightsTable.Remove(oldBookmark);
      _highlightsTable.Add(newBookmark, toUpdate);

      // новое форматирование стилем
      _styleService.ApplyStyle(newBookmark, toUpdate, newBookmark.Type);
    }

    /// <summary>
    /// Метод для удаления в OOXML документ аннотаций
    /// </summary>
    /// <param name="bookmark">Аннотация для удаления</param>
    public void DeleteBookmark(Bookmark bookmark)
    {
      if (!_highlightsTable.ContainsKey(bookmark))
        return;

      // удаляем из исходного документа
      var property = FindMatchingProperty(bookmark);
      bookmark.Range.Detach(property);

      // удаляем из потокового документа
      var toDelete = _highlightsTable[bookmark];
      toDelete.ToolTip = null;

      // удаляем локальную копию
      _highlightsTable.Remove(bookmark);

      // восстанавливаем прежнее форматирование
      _styleService.ApplyStyle(bookmark, toDelete);
    }

    /// <summary>
    /// Метод для обновления стилей аннотаций. Применяется при смене файла стилей.
    /// </summary>
    public void UpdateStyles()
    {
      foreach (var (bookmark, annotatedRun) in _highlightsTable)
      {
        _styleService.ApplyStyle(bookmark, annotatedRun, bookmark.Type);
      }
    }
  }
}