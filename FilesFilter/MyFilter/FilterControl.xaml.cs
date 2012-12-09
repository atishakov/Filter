using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;


namespace MyFilter
{
    /// <summary>
    /// Логика взаимодействия для FilterControl.xaml
    /// </summary>
    public partial class FilterControl : UserControl
    {
        
        public FilterControl(IFilter filter)
        {
            InitializeComponent();
            Filter = filter;
            ComboBoxProperties.ItemsSource = Filter.Properties;
        }

        private IFilter _Filter;
        public IFilter Filter
        {
            set
            {
                HeaderPanel.DataContext = value;
                FilterListBox.ItemsSource = value.CmpExpressions;
                _Filter = value;
            }
            get
            { return _Filter; }
        }

        private void btBack_Click(object sender, RoutedEventArgs e)
        {
            Filter = Filter.Parent;
            FilterListBox.Items.Refresh();
        }

        private void btBrackets_Click(object sender, RoutedEventArgs e)
        {
            Filter = Filter.PutIntoBrackets();
            FilterListBox.Items.Refresh();
        }

        private void btOpenExp_Click(object sender, RoutedEventArgs e)
        {
            int pID = (int)((Button)sender).Tag;
            Filter = Filter.GetSubFilter(pID);
            FilterListBox.Items.Refresh();
        }

        private void btAddItem_Click(object sender, RoutedEventArgs e)
        {
            int pID = (int)((Button)sender).Tag;
            Filter.AddSimpleExpression(pID);
            FilterListBox.Items.Refresh();
        }

        // 5
        private void btRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            int pID = (int)((Button)sender).Tag;
            Filter.RemoveExpression(pID);
            FilterListBox.Items.Refresh();
        }

        private void btAddExp_Click(object sender, RoutedEventArgs e)
        {
            Filter.AddCompoundExpression();
            FilterListBox.Items.Refresh();
        }
        // 11
        private void btAddSimpleExp_Click(object sender, RoutedEventArgs e)
        {
            Filter.AddSimpleExpression((System.Reflection.PropertyInfo)ComboBoxProperties.SelectedItem);
            FilterListBox.Items.Refresh();
        }
    }

    [ValueConversion(typeof(int), typeof(Visibility))]
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            int expType = (int)value;
            int prm = (int)System.Convert.ToInt32(parameter);

            if ((expType & prm) > 0)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }


    [ValueConversion(typeof(string), typeof(bool))]
    public class StrToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if ((string)value == "1")
                return true;
            else
                return false;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return "1";
            else
                return "0";

        }
    }

}
