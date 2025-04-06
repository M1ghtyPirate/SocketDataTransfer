using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SocketClient
{
	/// <summary>
	/// Interaction logic for PasswordPromptWindow.xaml
	/// </summary>
	public partial class PasswordPromptWindow : Window
	{

		public PasswordPromptWindow()
		{
			InitializeComponent();
		}

		#region Eventhandlers

		private void PasswordPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
			updateButtonAvailiability();
		}

		private void EnterPasswordButton_Click(object sender, RoutedEventArgs e) {
			this.DialogResult = true;
			this.Close();
		}

		private void EnterPasswordButton_Loaded(object sender, RoutedEventArgs e) {
			updateButtonAvailiability();
		}

		#endregion

		#region Methods

		private void updateButtonAvailiability() {
			if (this.PasswordPasswordBox == null || this.EnterPasswordButton == null) {
				return;
			}

			this.EnterPasswordButton.IsEnabled = this.PasswordPasswordBox.Password?.Any() ?? false;
		}

		#endregion
	}
}
