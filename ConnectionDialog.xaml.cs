using System.Windows;
using LINQPad.Extensibility.DataContext;
using System.Windows.Controls;
using System;

namespace DynamicWebServiceDriver
{
    /// <summary>
    /// Interaction logic for ConnectionDialog.xaml
    /// </summary>
    public partial class ConnectionDialog : Window
    {
        Properties _properties;

        public ConnectionDialog(IConnectionInfo cxInfo)
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }
                Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/PresentationFramework.Aero;V3.0.0.0;31bf3856ad364e35;component/themes/aero.normalcolor.xaml", UriKind.Relative)));
            }
            catch { }
            
            DataContext = _properties = new Properties(cxInfo);
            Background = SystemColors.ControlBrush;

            InitializeComponent();

            pwdBox.Password = _properties.Password;

        }

        void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _properties.Password = pwdBox.Password;
        }

        private void rdb_Checked(object sender, RoutedEventArgs e)
        {
            if (txtUsername != null)
            {
                txtUsername.Text = "";
                txtDomain.Text = "";
                pwdBox.Clear();
            }
        }


    }
}
