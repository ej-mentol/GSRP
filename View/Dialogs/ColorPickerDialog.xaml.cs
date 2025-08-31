using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GSRP.Views.Dialogs
{
    public partial class ColorPickerDialog : Window
    {
        public Color? SelectedColor { get; private set; }

        private bool _isUpdatingFromSliders = false;
        private readonly List<Color> _recentColors = new List<Color>();
        private readonly string _recentColorsFile;

        private readonly Color[] _predefinedColors = {
            // Vibrant colors
            Color.FromRgb(34, 197, 94),   // Green
            Color.FromRgb(59, 130, 246),  // Blue  
            Color.FromRgb(239, 68, 68),   // Red
            Color.FromRgb(245, 158, 11),  // Amber
            Color.FromRgb(168, 85, 247),  // Purple
            Color.FromRgb(236, 72, 153),  // Pink
            Color.FromRgb(20, 184, 166),  // Teal
            Color.FromRgb(251, 146, 60),  // Orange
            Color.FromRgb(139, 92, 246),  // Violet
            Color.FromRgb(244, 63, 94),   // Rose
            Color.FromRgb(16, 185, 129),  // Emerald
            Color.FromRgb(99, 102, 241),  // Indigo
            // Professional colors
            Color.FromRgb(148, 163, 184), // Slate
            Color.FromRgb(75, 85, 99),    // Dark Gray
            Color.FromRgb(17, 24, 39),    // Almost Black
            Colors.White,
        };

        public ColorPickerDialog(string title, Color? currentColor)
        {
            InitializeComponent();
            TitleText.Text = title;
            SelectedColor = currentColor;

            // Get path for recent colors file
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GSRP");
            Directory.CreateDirectory(appDataPath);
            _recentColorsFile = Path.Combine(appDataPath, "recent_colors.json");

            LoadRecentColors();
            PopulatePresets();
            PopulateRecentColors();

            // Set initial color
            if (currentColor.HasValue)
            {
                SetCurrentColor(currentColor.Value);
            }
            else
            {
                SetCurrentColor(Colors.Red); // Default
            }
        }

        private void LoadRecentColors()
        {
            try
            {
                if (File.Exists(_recentColorsFile))
                {
                    var json = File.ReadAllText(_recentColorsFile);
                    var colorStrings = JsonSerializer.Deserialize<List<string>>(json);

                    if (colorStrings != null)
                    {
                        foreach (var colorStr in colorStrings)
                        {
                            if (TryParseHexColor(colorStr, out var color))
                            {
                                _recentColors.Add(color);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors loading recent colors
            }
        }

        private void SaveRecentColors()
        {
            try
            {
                var colorStrings = new List<string>();
                foreach (var color in _recentColors)
                {
                    colorStrings.Add($"#{color.R:X2}{color.G:X2}{color.B:X2}");
                }

                var json = JsonSerializer.Serialize(colorStrings);
                File.WriteAllText(_recentColorsFile, json);
            }
            catch
            {
                // Ignore errors saving recent colors
            }
        }

        private void PopulatePresets()
        {
            foreach (var color in _predefinedColors)
            {
                var button = CreateColorButton(color);
                PresetsWrapPanel.Children.Add(button);
            }
        }

        private void PopulateRecentColors()
        {
            RecentWrapPanel.Children.Clear();
            foreach (var color in _recentColors)
            {
                var button = CreateColorButton(color);
                RecentWrapPanel.Children.Add(button);
            }
        }

        private Button CreateColorButton(Color color)
        {
            var button = new Button
            {
                Background = new SolidColorBrush(color),
                BorderBrush = GetBorderBrush(color),
                Tag = color,
                Style = (Style)FindResource("ColorButton")
            };

            // Set selected state
            if (SelectedColor.HasValue && ColorsEqual(SelectedColor.Value, color))
            {
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            }

            button.Click += PresetColorButton_Click;
            return button;
        }

        private bool ColorsEqual(Color c1, Color c2)
        {
            return c1.R == c2.R && c1.G == c2.G && c1.B == c2.B;
        }

        private Brush GetBorderBrush(Color color)
        {
            // For very light colors, use a darker border for visibility
            if (color.R > 240 && color.G > 240 && color.B > 240)
            {
                return new SolidColorBrush(Color.FromRgb(209, 213, 219));
            }

            // For very dark colors, use a lighter border
            if (color.R < 50 && color.G < 50 && color.B < 50)
            {
                return new SolidColorBrush(Color.FromRgb(75, 85, 99));
            }

            return Brushes.Transparent;
        }

        private void SetCurrentColor(Color color)
        {
            _isUpdatingFromSliders = true;

            // Update sliders and text boxes
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;

            RedTextBox.Text = color.R.ToString();
            GreenTextBox.Text = color.G.ToString();
            BlueTextBox.Text = color.B.ToString();

            HexTextBox.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";

            // Update preview
            ColorPreview.Background = new SolidColorBrush(color);

            _isUpdatingFromSliders = false;
        }

        private Color GetCurrentColor()
        {
            return Color.FromRgb(
                (byte)RedSlider.Value,
                (byte)GreenSlider.Value,
                (byte)BlueSlider.Value
            );
        }

        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingFromSliders) return;

            var color = GetCurrentColor();

            _isUpdatingFromSliders = true;

            // Update text boxes
            RedTextBox.Text = color.R.ToString();
            GreenTextBox.Text = color.G.ToString();
            BlueTextBox.Text = color.B.ToString();
            HexTextBox.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";

            // Update preview
            ColorPreview.Background = new SolidColorBrush(color);

            _isUpdatingFromSliders = false;
        }

        private void ColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromSliders) return;
            if (!(sender is TextBox textBox)) return;

            if (int.TryParse(textBox.Text, out var value) && value >= 0 && value <= 255)
            {
                _isUpdatingFromSliders = true;

                if (textBox == RedTextBox)
                    RedSlider.Value = value;
                else if (textBox == GreenTextBox)
                    GreenSlider.Value = value;
                else if (textBox == BlueTextBox)
                    BlueSlider.Value = value;

                var color = GetCurrentColor();
                HexTextBox.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
                ColorPreview.Background = new SolidColorBrush(color);

                _isUpdatingFromSliders = false;
            }
        }

        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromSliders) return;

            var hex = HexTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(hex)) return;

            if (TryParseHexColor(hex, out var color))
            {
                SetCurrentColor(color);
            }
        }

        private bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.Black;

            // Remove # if present
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            // Must be 6 characters
            if (hex.Length != 6)
                return false;

            try
            {
                var r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);

                color = Color.FromRgb(r, g, b);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void AddToRecentColors(Color color)
        {
            // Remove if already exists
            _recentColors.RemoveAll(c => ColorsEqual(c, color));

            // Add to beginning
            _recentColors.Insert(0, color);

            // Keep only last 12 colors
            if (_recentColors.Count > 12)
            {
                _recentColors.RemoveRange(12, _recentColors.Count - 12);
            }

            SaveRecentColors();
            PopulateRecentColors();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Handle case where mouse is not down when this is called
            }
        }

        private void PresetColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Color color)
            {
                SetCurrentColor(color);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var color = GetCurrentColor();
            SelectedColor = color;

            // Add to recent colors
            AddToRecentColors(color);

            DialogResult = true;
            Close();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedColor = null;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}