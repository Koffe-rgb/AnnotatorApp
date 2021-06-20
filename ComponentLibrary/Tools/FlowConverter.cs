using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

namespace ComponentLibrary.Tools
{
  /// <summary>
  /// Данный класс конвертирует OOXML документ в потоковый
  /// </summary>
  internal class FlowConverter : IDisposable
  {
    // тип отношения для поиска главной части документа
    private const string MainDocumentRelationshipType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";

    // пространства имен документа
    private const string
        WordprocessingMlNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
        RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // элементы и атрибуты OOXML документа
    private const string
      ParagraphElement = "p",
        RunPropertiesElement = "rPr",
        TableElement = "tbl",
        TableRowElement = "tr",
        TableCellElement = "tc",
        TableGridColElement = "gridCol",
        RunElement = "r",
        BreakElement = "br",
        TabCharacterElement = "tab",
        TextElement = "t",
        IndentationElement = "ind",
        AlignmentElement = "jc",
        ShadingElement = "shd",
        FillAttribute = "fill",
        BoldElement = "b",
        ItalicElement = "i",
        UnderlineElement = "u",
        StrikeElement = "strike",
        VerticalAlignmentElement = "vertAlign",
        ColorElement = "color",
        HighlightElement = "highlight",
        FontElement = "rFonts",
        FontSizeElement = "sz",
        RightToLeftElement = "rtl",
        PageBreakBeforeElement = "pageBreakBefore",
        SpacingElement = "spacing",
        ColorAttribute = "color",
        AsciiFontFamily = "ascii",
        SpacingAfterAttribute = "after",
        SpacingBeforeAttribute = "before",
        LeftIndentationAttribute = "left",
        RightIndentationAttribute = "right",
        HangingIndentationAttribute = "hanging",
        FirstLineIndentationAttribute = "firstLine",
        NameAttribute = "name",
        ListItemElement = "numPr",
        ListIdElement = "numId",
        ListItemDepthLevelElement = "ilvl",
        ValueAttribute = "w:val",
        FirstLineAttribute = "w:firstLine",
        ImageElement = "blip";


    private readonly Package _package;
    private readonly PackagePart _mainPart;
    private readonly Stream _mainPartStream;
    private readonly XmlReader _reader;

    private FlowDocument _document;


    /// <summary>
    /// Данный класс конвертирует OOXML документ в потоковый
    /// </summary>
    /// <param name="filePath">Путь до OOXML документа</param>
    public FlowConverter(string filePath)
    {
      _package = Package.Open(filePath, FileMode.Open, FileAccess.ReadWrite);
      // получение главной части документа, в которой записано содержимое документа
      var relationship = _package.GetRelationshipsByType(MainDocumentRelationshipType).First();
      _mainPart = _package.GetPart(PackUriHelper.CreatePartUri(relationship.TargetUri));
      _mainPartStream = _mainPart.GetStream();
      _reader = XmlReader.Create(_mainPartStream);
    }

    /// <summary>
    /// Данный метод конвертирует OOXML документ в потоковый документ
    /// </summary>
    /// <returns>Потоковый документ, построенный на основе OOXML документа</returns>
    public FlowDocument Convert()
    {
      Paragraph paragraph = null;
      Run run = null;
      List list = null;
      var listId = -1;

      // создание и настройка потокового документа
      _document = new FlowDocument
      {
        IsOptimalParagraphEnabled = true,
        IsHyphenationEnabled = true,
        IsColumnWidthFlexible = true
      };

      // парсинг OOXML документа и построение потокового
      while (_reader.Read())
      {
        if (_reader.NodeType == XmlNodeType.Element)
          switch (_reader.LocalName)
          {
            case ParagraphElement:      // считывание абзаца
              paragraph = new Paragraph
              {   // установка стандартных свойств
                TextIndent = 30,
                TextAlignment = TextAlignment.Left,
                Padding = new Thickness(0),
                Margin = new Thickness(5)
              };
              _document.Blocks.Add(paragraph);
              paragraph.Inlines.Add(new Span());
              break;

            case ListItemElement:       // считывание списка
              ReadList(paragraph, _document.Blocks, ref list, ref listId);
              break;

            case IndentationElement:     // считывание отступа абзаца
              ReadTextIndent(paragraph);
              break;

            case AlignmentElement: // считывание выравнивания текста
              ReadTextAlignment(paragraph);
              break;

            case RunElement:            // считвание прогона текста
              run = new Run();
              var span = paragraph.Inlines.FirstInline as Span;
              span.Inlines.Add(run);
              ReadRunElementProperties(run);
              break;

            case TextElement:           // считывание текста
              ReadText(run);
              break;

            case TableElement:          // считывание таблицы
              var table = new Table { Margin = new Thickness(5) };
              _document.Blocks.Add(table);
              ReadTable(table);
              break;

            case ImageElement:          // считывание изображения
              ReadImage(_document.Blocks, false);
              break;

            case TabCharacterElement:   // считывание табуляции
              run.Text += '\t';
              break;

            case BreakElement:          // считывание разрыва строки
              run.Text += "\r\n";
              break;
          }
      }
      this.Dispose();

      var emptySpans = _document.Blocks
        .Where(block => block is Paragraph)
          .Cast<Paragraph>()
          .Where(p =>
          {
            if (p.Inlines.FirstInline is not Span span) return false;
            if (span.Inlines.Count != 1) return false;
            return span.Inlines.FirstInline is Run r && string.IsNullOrWhiteSpace(r.Text);
          })
          .ToList();


      foreach (var emptySpan in emptySpans)
        _document.Blocks.Remove(emptySpan);

      return _document;
    }

    /// <summary>
    /// Данный метод считывает таблицу из OOXML-документа
    /// в переданную параметром таблицу потокового документа
    /// </summary>
    /// <param name="table">Таблица потокового документа, 
    /// в которую помещается считанная информация</param>
    private void ReadTable(Table table)
    {
      TableRow currentRow = null;
      TableCell currentCell = null;
      Paragraph para = null;
      Run run = null;
      List list = null;
      var cellNumber = 0;
      var id = -1;

      table.Background = Brushes.Gray;
      _reader.Read();

      while ((_reader.NodeType != XmlNodeType.EndElement
          || _reader.LocalName != TableElement) && _reader.Read())
      {
        if (_reader.NodeType == XmlNodeType.Element)
        {
          switch (_reader.LocalName)
          {
            case TableGridColElement:   // считывание столбцов
              table.Columns.Add(new TableColumn());
              break;

            case TableRowElement:       // считывание строк
              table.RowGroups.Add(new TableRowGroup());
              currentRow = new TableRow();
              table.RowGroups[0].Rows.Add(currentRow);
              cellNumber = 0;
              break;

            case TableCellElement:      // считывание ячейки таблицы
              currentCell = new TableCell
              {
                Background = Brushes.White,
                Padding = new Thickness(5)
              };
              cellNumber++;
              if (currentRow != null)
              {
                currentRow.Cells.Add(currentCell);
                currentRow.Cells[0].ColumnSpan = table.Columns.Count - cellNumber + 1;
              }
              break;

            case ParagraphElement:      // считывание абзаца из ячейки
              para = new Paragraph();
              currentCell?.Blocks.Add(para);
              break;

            case RunElement:            // считывание прогона текста
              run = new Run();
              para?.Inlines.Add(run);
              ReadRunElementProperties(run);
              break;

            case TextElement:           // считывание текста
              ReadText(run); break;

            case ListItemElement:       // считывание списка
              if (currentCell != null)
                ReadList(para, currentCell.Blocks, ref list, ref id);
              break;

            case IndentationElement:     // считывание отступа
              ReadTextIndent(para);
              break;

            case AlignmentElement: // считывание выравнивания текста
              if (para != null)
                ReadTextAlignment(para);
              break;

            case ImageElement:          // считывание изображения из ячейки
              if (currentCell != null)
                ReadImage(currentCell.Blocks, true);
              break;
          }
        }
      }
    }

    /// <summary>
    /// Данный метод считывает прогон текста из OOXML-документа
    /// в переданный параметром прогон потокового документа
    /// </summary>
    /// <param name="run">Прогон текста потокового документа,
    /// в который помещается считанная информация</param>
    private void ReadText(Run run)
    {
      var spacePreserve = false;

      while (_reader.NodeType != XmlNodeType.EndElement || _reader.LocalName != TextElement)
      {
        if (_reader.HasAttributes)
        {
          _reader.MoveToAttribute("xml:space");
          if (_reader.Value == "preserve")
            spacePreserve = true;

          // для пустых t элементов, в которых хранится пробел (иногда возникает в документе)
          if (_reader.Read() && _reader.NodeType != XmlNodeType.Text && spacePreserve)
          {
            run.Text += " ";
            spacePreserve = false;
          }
          continue;
        }

        if (_reader.NodeType == XmlNodeType.Text)
        {
          run.Text += _reader.Value;
        }

        spacePreserve = false;
        _reader.Read();
      }
    }

    /// <summary>
    /// Данный метод считывает отступ текста в абзаце из OOXML-документа
    /// в переданный параметром абзац потокового документа
    /// </summary>
    /// <param name="para">Абзац потокового документа, для
    /// которого считывается отступ текста</param>
    private void ReadTextIndent(Paragraph para)
    {
      if (!_reader.HasAttributes) return;
      _reader.MoveToAttribute(FirstLineAttribute);
      var isParsed = double.TryParse(_reader.Value, out double ind);
      if (isParsed)
        para.TextIndent = ind / 23.6;
    }

    /// <summary>
    /// Данный метод считывает выравнивание текста из OOXML-документа
    /// в переданный параметром блочный элемент потокового документа
    /// </summary>
    /// <param name="block">Блочный элемент потокового документа, для
    /// которого считывается выравнивание</param>
    private void ReadTextAlignment(Block block)
    {
      if (!_reader.HasAttributes) return;
      _reader.MoveToAttribute(ValueAttribute);
      block.TextAlignment = _reader.Value switch
      {
        "left" => TextAlignment.Left,
        "right" => TextAlignment.Right,
        "center" => TextAlignment.Center,
        "both" => TextAlignment.Justify,
        _ => block.TextAlignment
      };
    }

    /// <summary>
    /// Данный метод считывает список из OOXML-документа
    /// в переданный параметром список потокового документа
    /// </summary>
    /// <param name="para">Абзац потокового документа,
    /// в который помещается список</param>
    /// <param name="blocks">Коллекция блочных элементов 
    /// потокового документа, в которой хранится список</param>
    /// <param name="list">Список потокового документа, 
    /// в который помещается считанная информация</param>
    /// <param name="curId">Идентификатор последнего списка 
    /// из OOXML-документа</param>
    private void ReadList(Paragraph para, BlockCollection blocks, ref List list, ref int curId)
    {
      var id = -1;
      para.TextIndent = 0;
      var listItem = new ListItem(para);
      _document.Blocks.Remove(para);

      while ((_reader.NodeType != XmlNodeType.EndElement
          || _reader.LocalName != ListItemElement) && _reader.Read())
      {
        switch (_reader.LocalName)
        {
          case ListItemDepthLevelElement:     // считывание уровня элемента списка
            _reader.MoveToAttribute(ValueAttribute);
            var ilvl = int.Parse(_reader.Value) + 1;
            listItem.Margin = new Thickness(ilvl * 20, 0, 0, 0);
            break;
          case ListIdElement:                 // считывание идентификатора списка
            _reader.MoveToAttribute(ValueAttribute);
            id = int.Parse(_reader.Value);
            break;
        }
      }
      if (curId != id)
      {
        curId = id;
        list = new List
        {
          Padding = new Thickness(0),
          TextAlignment = TextAlignment.Left,
          Margin = new Thickness(5)
        };
        list.ListItems.Add(listItem);

        blocks.Add(list);
      }
      else
      {
        list?.ListItems.Add(listItem);
      }
    }

    /// <summary>
    /// Данный метод считывает изображение из OOXML-документа
    /// в переданную параметром коллекцию блочных элементов потокового документа
    /// </summary>
    /// <param name="blocks">Коллекция блочных элементов потокового документа, 
    /// в которую помещается считанное изображение</param>
    /// <param name="toTable">Флаг, определяющий добавляется ли изображение в таблицу</param>
    private void ReadImage(BlockCollection blocks, bool toTable)
    {
      if (!_reader.HasAttributes) return;
      _reader.MoveToAttribute("r:embed");

      var rId = _reader.Value;
      var imageRelationship = _mainPart.GetRelationship(rId);
      var imageUri = imageRelationship.TargetUri;
      Image image = null;
      var isFound = false;

      foreach (var part in _package.GetParts()) // поиск части документа, в которой хранятся изображения
      {
        if (part.ContentType.ToLowerInvariant().StartsWith("image/"))
        {
          if (part.Uri.ToString().EndsWith(imageUri.ToString()))
          {
            var ms = new MemoryStream();
            using (var source = part.GetStream(FileMode.Open, FileAccess.Read))  // считывание изображения
            {
              source.CopyTo(ms);
              ms.Seek(0, SeekOrigin.Begin);
              try
              {
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.Default);
                image = new Image { Source = decoder.Frames[0] };
              }
              catch
              {
                break;
              }
            }
            isFound = true;
          }
        }
        if (isFound)
          break;
      }

      if (!isFound)
      {
        MessageBox.Show("Не удалось загрузить изображение");
        return;
      }

      // настройка отображения изображения

      image.Stretch = Stretch.None;
      var figure = new Figure();
      if (toTable)  // различные высота и ширина для таблиц и не-таблиц
      {
        figure.Height = new FigureLength(0.5, FigureUnitType.Auto);
        figure.Width = new FigureLength(0.5, FigureUnitType.Auto);
      }
      else
      {
        figure.Height = new FigureLength(0.8, FigureUnitType.Page);
        figure.Width = new FigureLength(0.8, FigureUnitType.Page);
      }

      figure.HorizontalAnchor = FigureHorizontalAnchor.ColumnCenter;
      figure.WrapDirection = WrapDirection.None;
      figure.Margin = new Thickness(5);
      figure.Blocks.Add(new BlockUIContainer(image));
      blocks.Add(new Paragraph(figure));
    }

    /// <summary>
    /// Метод, считывающий атрибут <i>val</i> у текущей вершины парсера reader
    /// </summary>
    /// <param name="reader">Парсер OOXML документа</param>
    /// <returns>Значение атрибцта <i>val</i> у текущей вершины</returns>
    private static string ReadValueAttribute(XmlReader reader)
    {
      return reader["val", WordprocessingMlNamespace];
    }

    /// <summary>
    /// Метод, считывающий значения различных флагов (по типу true и false)
    /// </summary>
    /// <param name="reader">Парсер OOXML документа</param>
    /// <returns>Значение флага</returns>
    private static bool ReadOnOffValueAttribute(XmlReader reader)
    {
      var value = ReadValueAttribute(reader);

      return value switch
      {
        null => true,
        "1" => true,
        "on" => true,
        "true" => true,
        _ => false
      };
    }

    /// <summary>
    /// Метод, считывающий параметры подчеркивания и конвертирующий их
    /// в версию для потокового документа
    /// </summary>
    /// <param name="reader">Парсер OOXML документа</param>
    /// <param name="inline">Строковый контейнер потокового документа</param>
    /// <returns>Настройки форматирования подчеркивания для элементов
    /// потокового документа</returns>
    private static TextDecorationCollection ReadUnderlineTextDecorations(XmlReader reader, Inline inline)
    {
      TextDecoration textDecoration;
      var color = ConvertColor(reader[ColorAttribute, WordprocessingMlNamespace]);

      var brush = color.HasValue ?
          new SolidColorBrush(color.Value) :
          inline.Foreground;

      var textDecorations = new TextDecorationCollection
          {
              (textDecoration = new TextDecoration
              {
                  Location = TextDecorationLocation.Underline,
                  Pen = new Pen
                  {
                      Brush = brush
                  }
              })
          };

      switch (ReadValueAttribute(reader))
      {
        case "single":
          break;

        case "double":
          textDecoration.PenOffset = inline.FontSize * 0.05;
          textDecoration = textDecoration.Clone();
          textDecoration.PenOffset = inline.FontSize * -0.05;
          textDecorations.Add(textDecoration);
          break;

        case "dotted":
          textDecoration.Pen.DashStyle = DashStyles.Dot;
          break;

        case "dash":
          textDecoration.Pen.DashStyle = DashStyles.Dash;
          break;

        case "dotDash":
          textDecoration.Pen.DashStyle = DashStyles.DashDot;
          break;

        case "dotDotDash":
          textDecoration.Pen.DashStyle = DashStyles.DashDotDot;
          break;

        default:
          return null;
      }

      return textDecorations;
    }

    /// <summary>
    /// Метод, конвертирующий их в версию для элементов потокового документа
    /// </summary>
    /// <param name="verticalAlignmentString">Значение настройки надстрочных и
    /// подстрочных элементов в OOXML документе</param>
    /// <returns>Настройки форматирования надстрочных и подстрочных элементов
    /// потокового документа</returns>
    private static BaselineAlignment? ConvertBaselineAlignment(string verticalAlignmentString)
    {
      return verticalAlignmentString switch
      {
        "baseline" => BaselineAlignment.Baseline,
        "subscript" => BaselineAlignment.Subscript,
        "superscript" => BaselineAlignment.Superscript,
        _ => null
      };
    }

    /// <summary>
    /// Метод, конвертирующий настройки цвета OOXML документа в версию для элементов
    /// потокового документа
    /// </summary>
    /// <param name="colorString">Значение цвета</param>
    /// <returns>Объект цвета, который может быть применен к элементам потокового документа</returns>
    private static Color? ConvertColor(string colorString)
    {
      if (string.IsNullOrEmpty(colorString) || colorString == "auto")
        return null;

      return (Color)ColorConverter.ConvertFromString('#' + colorString);
    }

    /// <summary>
    /// Метод, конвертирующий настройки цвета подсветки OOXML документа в версию для
    /// элементов потокового документа
    /// </summary>
    /// <param name="highlightString">Значение цвета подсветки текста</param>
    /// <returns>Объект цвета, который может быть применен к элементам потокового документа</returns>
    private static Color? ReadHighlightColor(string highlightString)
    {
      if (string.IsNullOrEmpty(highlightString) || highlightString == "auto")
        return null;

      return (Color)ColorConverter.ConvertFromString(highlightString);
    }

    /// <summary>
    /// Метод считывающий информацию о настройках прогона текста в OOXML документе
    /// в переданный строковый контейнер потокового документа
    /// </summary>
    /// <param name="inline">Строковый контейнер потокового документа</param>
    private void ReadRunElementProperties(Inline inline)
    {
      while (_reader.NodeType != XmlNodeType.EndElement && _reader.Read())
      {
        if (_reader.NodeType == XmlNodeType.Element && _reader.NamespaceURI == WordprocessingMlNamespace)
        {
          switch (_reader.LocalName)
          {
            case BoldElement:
              inline.FontWeight = ReadOnOffValueAttribute(_reader) ?
                  FontWeights.Bold :
                  FontWeights.Normal;
              break;

            case ItalicElement:
              inline.FontStyle = ReadOnOffValueAttribute(_reader) ?
                  FontStyles.Italic :
                  FontStyles.Normal;
              break;

            case UnderlineElement:
              var underlineDecor = ReadUnderlineTextDecorations(_reader, inline);
              if (underlineDecor != null)
                inline.TextDecorations.Add(underlineDecor);
              break;

            case StrikeElement:
              if (ReadOnOffValueAttribute(_reader))
                inline.TextDecorations.Add(TextDecorations.Strikethrough);
              break;

            case VerticalAlignmentElement:
              var baselineAlignment = ConvertBaselineAlignment(ReadValueAttribute(_reader));
              if (baselineAlignment.HasValue)
              {
                inline.BaselineAlignment = baselineAlignment.Value;
                if (baselineAlignment.Value == BaselineAlignment.Subscript || baselineAlignment.Value == BaselineAlignment.Superscript)
                  inline.FontSize *= 0.65;
              }
              break;

            case ColorElement:
              var color = ConvertColor(ReadValueAttribute(_reader));
              if (color.HasValue)
                inline.Foreground = new SolidColorBrush(color.Value);
              break;

            case HighlightElement:
              var highlight = ReadHighlightColor(ReadValueAttribute(_reader));
              if (highlight.HasValue)
                inline.Background = new SolidColorBrush(highlight.Value);
              break;

            case FontElement:
              var fontFamily = _reader[AsciiFontFamily, WordprocessingMlNamespace];
              if (!string.IsNullOrEmpty(fontFamily))
                inline.FontFamily = (FontFamily)new FontFamilyConverter().ConvertFromString(fontFamily);
              break;

            case FontSizeElement:
              var fontSize = _reader["val", WordprocessingMlNamespace];
              if (!string.IsNullOrEmpty(fontSize))
                inline.FontSize = uint.Parse(fontSize) * 0.6666666666666667;
              break;

            case RightToLeftElement:
              inline.FlowDirection = ReadOnOffValueAttribute(_reader) ?
                  FlowDirection.RightToLeft :
                  FlowDirection.LeftToRight;
              break;
          }
        }
      }
    }

    public void Dispose()
    {
      ((IDisposable)_package)?.Dispose();
      _mainPartStream?.Dispose();
      _reader?.Dispose();
    }
  }
}
