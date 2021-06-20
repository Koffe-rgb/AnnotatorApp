using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ComponentLibrary.Tools;
using Color = System.Drawing.Color;
using XamlWriter = System.Windows.Markup.XamlWriter;


namespace ComponentLibrary
{
    /// <summary>
    /// Класс редактора стилей
    /// </summary>
    public partial class StyleEditor
    {
        public ObservableCollection<DependencyProperty> Properties { get; }
        public ObservableCollection<object> Values { get; }
        public object SelectedValue { get; set; }

        private DependencyProperty _selectedProperty;
        public DependencyProperty SelectedProperty
        {
            get => _selectedProperty;
            set
            {
                _selectedProperty = value;
                Values.Clear();
                var valuesForSelectedProperty = _propertyValuesDictionary[_selectedProperty];
                valuesForSelectedProperty.ForEach(o => Values.Add(o));
            }
        }

        public ObservableCollection<string> Styles { get; set; }
        private int _forLast;

        private Style _selectedStyle = UserStyleService.DefaultStyle;

        private readonly Dictionary<DependencyProperty, List<object>> _propertyValuesDictionary;
        private readonly ResourceDictionary _resourceDictionary;

        public StyleEditor(ResourceDictionary styles)
        {
            Properties = new ObservableCollection<DependencyProperty>(new List<DependencyProperty>
            {
                TextElement.FontFamilyProperty,
                TextElement.FontStyleProperty,
                TextElement.FontWeightProperty,
                TextElement.FontStretchProperty,
                TextElement.FontSizeProperty,
                TextElement.ForegroundProperty,
                TextElement.BackgroundProperty
            });

            _resourceDictionary = styles;
            Styles = new ObservableCollection<string>();
            UpdateListOfStyles(styles);

            Values = new ObservableCollection<object>();

            _propertyValuesDictionary = new Dictionary<DependencyProperty, List<object>>();
            foreach (var property in Properties)
            {
                var propertyValues = property.Name switch
                {
                    "FontSize" => Enumerable.Range(10, 21).Cast<object>().ToList(),
                    "FontFamily" => Fonts.SystemFontFamilies.Cast<object>().ToList(),
                    "FontStyle" => typeof(FontStyles).GetProperties().Cast<object>().ToList(),
                    "FontWeight" => typeof(FontWeights).GetProperties().Cast<object>().ToList(),
                    "FontStretch" => typeof(FontStretches).GetProperties().Cast<object>().ToList(),
                    "Foreground" => typeof(KnownColor).GetEnumValues().Cast<object>().ToList(),
                    "Background" => typeof(KnownColor).GetEnumValues().Cast<object>().ToList(),
                    _ => null
                };

                _propertyValuesDictionary.Add(property, propertyValues);
            }

            StyledRun = new Run("Съешь ещё этих мягких французских булок, да выпей же чаю")
            {
                Style = UserStyleService.DefaultStyle
            };

            InitializeComponent();
        }

        private void UpdateValuesForSelectedProperty()
        {
            var setter = _selectedStyle.Setters
                .Cast<Setter>()
                .FirstOrDefault(s => s.Property == _selectedProperty);

            var namesOfValues = _propertyValuesDictionary[_selectedProperty].Select(o => o.ToString());

            if (setter != null)
            {

                var valueStr = setter.Value.ToString();
                var isColorHex = Regex.IsMatch(valueStr, @"[#]([0-9]|[a-f]|[A-F]){8}\b");

                if (isColorHex)
                {
                    valueStr = ColorTranslator.FromHtml(valueStr).ToKnownColor().ToString();
                }
                else if (setter.Value is not System.Windows.Media.FontFamily)
                {
                    namesOfValues = namesOfValues.Select(s => s.Split().Last());
                }

                CbxValues.SelectedIndex = namesOfValues
                    .TakeWhile(name => !string.Equals(name, valueStr, StringComparison.OrdinalIgnoreCase))
                    .Count();
            }
            else
            {
                CbxValues.SelectedIndex = namesOfValues
                    .TakeWhile(name => !string.Equals(name,
                        UserStyleService.DefaultStyle.Setters.Cast<Setter>()
                            .FirstOrDefault(s => s.Property == _selectedProperty)?
                            .Value.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                    .Count();
            }
        }

        private void UpdateListOfStyles(ResourceDictionary styles)
        {
            var list = styles.Keys.Cast<string>().ToList();
            list.Sort();
            var lastOrDefault = list.LastOrDefault(s => s.StartsWith("New Style "));
            if (lastOrDefault == null)
                _forLast = 1;
            else
                _forLast = int.Parse(lastOrDefault.Split().Last()) + 1;
            Styles.Clear();
            list.ForEach(s => Styles.Add(s));
        }

        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            var save = XamlWriter.Save(_resourceDictionary);
            File.WriteAllText(UserStyleService.FilePath, save);
            DialogResult = true;
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnAddStyle_OnClick(object sender, RoutedEventArgs e)
        {
            var styleToAdd = new Style(typeof(Run), UserStyleService.DefaultStyle);
            _resourceDictionary.Add($"New Style {_forLast}", styleToAdd);
            UpdateListOfStyles(_resourceDictionary);
        }

        private void BtnRemoveStyle_OnClick(object sender, RoutedEventArgs e)
        {
            var idx = LvStyles.SelectedIndex;
            var styleToDelete = LvStyles.SelectedItem.ToString();
            LvStyles.SelectedIndex = -1;
            Styles.Remove(styleToDelete);
            LvStyles.SelectedIndex = idx - 1 >= 0 ? idx - 1 : 0;
            _resourceDictionary.Remove(styleToDelete);
        }

        private void LvProperties_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateValuesForSelectedProperty();
        }

        private void LvStyles_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvStyles.SelectedIndex == -1)
                return;

            _selectedStyle = _resourceDictionary[LvStyles.SelectedItem.ToString()] as Style;
            StyledRun.Style = _selectedStyle;
            TextBoxStyleName.Text = LvStyles.SelectedItem.ToString();
            LvProperties.SelectedIndex = 0;
            UpdateValuesForSelectedProperty();
        }

        private void CbxValues_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvStyles.SelectedItem == null || SelectedValue ==  null)
                return;

            var actualValue = SelectedValue switch
            {
                PropertyInfo info => info.GetMethod?.Invoke(null, BindingFlags.GetProperty, null, null, null),
                int integer => integer * 1.0,
                KnownColor c => new SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(
                        Color.FromKnownColor(c).A, Color.FromKnownColor(c).R, 
                        Color.FromKnownColor(c).G, Color.FromKnownColor(c).B
                        )
                    ),
                _ => SelectedValue
            };

            var originalSetters = _selectedStyle.Setters.Cast<Setter>().ToList();
            var changedSetter = originalSetters.Find(setter => setter.Property == _selectedProperty);

            originalSetters.Remove(changedSetter);
            originalSetters.Add(new Setter
            {
                Property = _selectedProperty,
                Value = actualValue
            });

            _selectedStyle = new Style(typeof(Run));
            originalSetters.ForEach(setter => _selectedStyle.Setters.Add(setter));
            _resourceDictionary[LvStyles.SelectedItem.ToString()] = _selectedStyle;

            StyledRun.Style = _selectedStyle;
        }

        private void BtnChangeStyleName_OnClick(object sender, RoutedEventArgs e)
        {
            var name = TextBoxStyleName.Text.Trim();
            var style = _selectedStyle;
            var styleToDelete = LvStyles.SelectedItem.ToString();
            LvStyles.SelectedIndex = -1;
            _resourceDictionary.Remove(styleToDelete);
            _resourceDictionary.Add(name, style);
            UpdateListOfStyles(_resourceDictionary);
            LvStyles.SelectedIndex = Styles
                .TakeWhile(s => !string.Equals(s, name, 
                    StringComparison.OrdinalIgnoreCase))
                .Count();
        }
    }
}
