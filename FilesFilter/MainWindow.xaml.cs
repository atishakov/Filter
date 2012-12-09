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
using System.IO;
using System.Xml.Linq;
using MyFilter;

namespace FilesFilter
{
    /// <summary>
    /// Логика взаимодействия для Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();            

            if (File.Exists("filter.xml"))
            {
                _filter = new Filter<FileInfo>(false, XElement.Load("filter.xml"));                
            }
            else
                _filter = new Filter<FileInfo>(false);
            
            Filter = new FilterControl(_filter);
            GroupBoxFilter.Content = Filter;
            Filter.Margin = new Thickness(0, 0, 0, 0);
            Filter.VerticalAlignment = VerticalAlignment.Top;
        }

        FilterControl Filter;
        Filter<FileInfo> _filter;

        
        private IEnumerable<FileInfo> CheckDirectory(string directory)
        {
            string[] files = Directory.GetFiles(directory);

            foreach (string file in files)
            {
                yield return new FileInfo(file);               
            }
            
            string[] directories = System.IO.Directory.GetDirectories(directory);

            foreach (string dir in directories)
            {
                foreach (FileInfo fi in CheckDirectory(dir))
                    yield return fi;
            }            
        }              

        private void btLookUp_Click(object sender, RoutedEventArgs e)
        {
            Func<FileInfo, bool> func = _filter.BuildExpression().Compile();
            StatDataGrid.ItemsSource = 
                (from f in  CheckDirectory(DirName.Text)
                 where func(f) select f).ToList<FileInfo>();
            StatDataGrid.Items.Refresh();
        }

        private void btQueryText_Click(object sender, RoutedEventArgs e)
        {
            QueryTextRTB.Document.Blocks.Clear();
            QueryTextRTB.AppendText(Filter.Filter.QueryText());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {            
            _filter.GetXMLRoot().Save("filter.xml");
        }

    }
}
