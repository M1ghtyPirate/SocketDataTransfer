using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
using System.Windows.Threading;

namespace SocketClient {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		#region Properties

		/// <summary>
		/// Параметры для открытия сокета заполнены
		/// </summary>
		private bool parametersFilled { get => !string.IsNullOrWhiteSpace(this.IPAddressTextBox?.Text) && !string.IsNullOrWhiteSpace(this.PortTextBox?.Text) && this.StudentInfoHashLabel?.Tag != null; }

		/// <summary>
		/// Сокет соединения
		/// </summary>
		private Socket socket { get; set; }

		/// <summary>
		/// Соединение открыто
		/// </summary>
		private bool isConnected { get => this.socket?.Connected ?? false; }

		/// <summary>
		/// Служебные сообщения
		/// </summary>
		private static class Responses {
			public static string ACK = "<|ACK|>";
			public static string NACK = "<|NACK|>";
		}

		#endregion


		public MainWindow() {
			InitializeComponent();
		}

		#region EventHandlers

		private void IPAddressTextBox_TextChanged(object sender, TextChangedEventArgs e) {
			updateButtonsAvailiability();
		}

		private void PortTextBox_TextChanged(object sender, TextChangedEventArgs e) {
			updateButtonsAvailiability();
		}

		private void StudentInfoTextBox_TextChanged(object sender, TextChangedEventArgs e) {
			updateStudentInfoHash();
			updateButtonsAvailiability();
		}

		private void StudentInfoHashLabel_Loaded(object sender, RoutedEventArgs e) {
			updateStudentInfoHash();
			updateButtonsAvailiability();
		}

		private void ConnectButton_Click(object sender, RoutedEventArgs e) {
			connect();
			updateButtonsAvailiability();
			updateTextBoxesAvailiability();
		}

		private void DisconnectButton_Click(object sender, RoutedEventArgs e) {
			disconnect();
			updateButtonsAvailiability();
			updateTextBoxesAvailiability();
		}

		private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e) {
			updateButtonsAvailiability();
		}

		private void SendButton_Click(object sender, RoutedEventArgs e) {
			sendMessage();
			updateButtonsAvailiability();
		}

		private void MessageTextBox_KeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) > 0) {
				e.Handled = true;
				sendMessage();
				updateButtonsAvailiability();
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Запись сообщения в поле поле лога
		/// </summary>
		/// <param name="message"></param>
		private void logMessage(string message, string messageSource = null) {
			if (this.MessageLogTextBlock == null) {
				return;
			}

			var formatedMessage = $"{(string.IsNullOrWhiteSpace(this.MessageLogTextBlock.Text) ? "" : "\n")}[{DateTime.Now.ToString("dd.MM.yyyy hh:mm:ss")}] :{(messageSource == null ? "" : $" ({messageSource})")} {message ?? ""}";
			this.MessageLogTextBlock.Text += formatedMessage;
			this.MessageLogScrollViewer.ScrollToEnd();
		}

		/// <summary>
		/// Обновление хэша данных о студенте
		/// </summary>
		private void updateStudentInfoHash() {
			if (this.StudentInfoTextBox == null || this.StudentInfoHashLabel == null) {
				return;
			}

			// Хэш для варианта 9 - сумма кодов первой, средней и последней букв
			int? hashValue = string.IsNullOrWhiteSpace(this.StudentInfoTextBox.Text) ? 
				null :
				(int)this.StudentInfoTextBox.Text.First() + (int)this.StudentInfoTextBox.Text[this.StudentInfoTextBox.Text.Length / 2] + (int)this.StudentInfoTextBox.Text.Last();
			var hash = !hashValue.HasValue ? null : Encoding.UTF8.GetBytes(hashValue + "");
			this.StudentInfoHashLabel.Tag = hash;
			this.StudentInfoHashLabel.Content = hash == null ? "" : Convert.ToHexString(hash);
		}

		/// <summary>
		/// Обновление доступности кнаопок старта и окончания прослушивания
		/// </summary>
		private void updateButtonsAvailiability() {
			if (this.ConnectButton == null || this.DisconnectButton == null || this.SendButton == null) {
				return;
			}

			this.ConnectButton.IsEnabled = parametersFilled && !isConnected;
			this.DisconnectButton.IsEnabled = isConnected;
			this.SendButton.IsEnabled = isConnected && !string.IsNullOrWhiteSpace(this.MessageTextBox.Text);
		}

		/// <summary>
		/// Обновление доступности полей ввода параметров
		/// </summary>
		private void updateTextBoxesAvailiability() {
			if (this.IPAddressTextBox == null || this.PortTextBox == null || this.StudentInfoTextBox == null) {
				return;
			}

			this.IPAddressTextBox.IsEnabled = this.PortTextBox.IsEnabled = this.StudentInfoTextBox.IsEnabled = !isConnected;
		}

		/// <summary>
		/// Получение подтверждения от сервера
		/// </summary>
		/// <returns></returns>
		private bool getAcknowledgement() {
			var buffer = new byte[256];
			var data = new List<byte>();
			var bytesRead = 0;
			do {
				bytesRead = this.socket.Receive(buffer);
				data.AddRange(buffer.ToList().GetRange(0, bytesRead));
			} while (this.socket.Available > 0);
			var response = Encoding.UTF8.GetString(data.ToArray());
			this.Dispatcher.Invoke(() => logMessage($"Получен ответ: <{response}>", this.socket?.RemoteEndPoint?.ToString()));
			return response == Responses.ACK;
		}

		/// <summary>
		/// Запрос пароля
		/// </summary>
		/// <returns></returns>
		private string promptPassword() {
			var passwordPrompt = new PasswordPromptWindow();
			passwordPrompt.Owner = this;
			var result = passwordPrompt.ShowDialog();
			return (result ?? false) ? passwordPrompt.PasswordPasswordBox.Password : null;
		}

		/// <summary>
		/// Отправка сообщения
		/// </summary>
		private void sendMessage() {
			if (this.MessageTextBox == null || !isConnected) {
				return;
			}

			this.Dispatcher.Invoke(() => logMessage($"Отправка сообщения: <{this.MessageTextBox.Text}>", this.socket?.RemoteEndPoint?.ToString()));
			try {
				this.socket.Send(Encoding.UTF8.GetBytes(this.MessageTextBox.Text));
			} catch (Exception ex) {
				this.Dispatcher.Invoke(() => logMessage(ex.ToString(), this.socket?.RemoteEndPoint?.ToString()));
				disconnect();
				return;
			}

			// Ждем подтверждения
			var response = getAcknowledgement();
			if (!response) {
				disconnect();
				return;
			}

			this.MessageTextBox.Text = null;
		}

		/// <summary>
		/// Проверка соединения
		/// </summary>
		/// <returns></returns>
		private bool checkConnection(Socket handler, bool logFailure = false) {
			if (handler == null) {
				return false;
			}

			var blockingState = handler.Blocking;
			handler.Blocking = false;
			var connected = true;

			try {
				handler.Send(new byte[1], 0, 0);
			} catch (Exception ex) {
				if (logFailure) {
					this.Dispatcher.Invoke(() => logMessage($"Сервер не отвечает", handler?.RemoteEndPoint?.ToString()));
				}
				connected = false;
			}

			handler.Blocking = blockingState;
			return connected;
		}

		/// <summary>
		/// Подключение к сокету
		/// </summary>
		private void connect() {
			if (!parametersFilled) {
				logMessage("Не заполнены параметры.");
				return;
			}

			if (!IPAddress.TryParse(this.IPAddressTextBox.Text, out var ip) || !int.TryParse(this.PortTextBox.Text, out var port)) {
				logMessage($"Не удалось сформировать адрес для открытия сокета: <{this.IPAddressTextBox.Text}>:<{this.PortTextBox.Text}>");
				return;
			}

			var ipEndpoint = new IPEndPoint(ip, port);
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try {
				this.socket.Connect(ipEndpoint);
			} catch(Exception ex) {
				logMessage(ex + "");
				return;
			}
			
			logMessage($"Соединение открыто", this.socket?.RemoteEndPoint?.ToString());
			var buffer = new byte[256];
			var data = new List<byte>();
			var bytesRead = 0;
			bool response;
			// Отправка данных о студенте
			this.Dispatcher.Invoke(() => logMessage($"Отправка данных о студенте: <{this.StudentInfoTextBox.Text}>", this.socket?.RemoteEndPoint?.ToString()));
			data = Encoding.UTF8.GetBytes(this.StudentInfoTextBox.Text).ToList();
			this.socket.Send(data.ToArray());
			data.Clear();

			// Ждем подтверждения
			response = getAcknowledgement();
			if (!response) {
				disconnect();
				return;
			}

			// Отправка хэша данных
			this.Dispatcher.Invoke(() => logMessage($"Отправка хэша данных: <{this.StudentInfoHashLabel.Content}>", this.socket?.RemoteEndPoint?.ToString()));
			data = ((byte[])this.StudentInfoHashLabel.Tag).ToList();
			this.socket.Send(data.ToArray());
			data.Clear();

			// Ждем подтверждения
			response = getAcknowledgement();
			if (!response) {
				disconnect();
				return;
			}

			// Отправка хэша пароля
			var password = promptPassword();
			if (password == null) {
				this.Dispatcher.Invoke(() => logMessage($"Пароль не введен", this.socket?.RemoteEndPoint?.ToString()));
				disconnect(); 
				return;
			}
			data = MD5.HashData(Encoding.UTF8.GetBytes(password)).ToList();
			this.Dispatcher.Invoke(() => logMessage($"Отправка хэша пароля: <{Convert.ToHexString(data.ToArray())}>", this.socket?.RemoteEndPoint?.ToString()));
			this.socket.Send(data.ToArray());
			data.Clear();

			// Ждем подтверждения
			response = getAcknowledgement();
			if (!response) {
				disconnect();
				return;
			}

			// Ожидание сообщения о закрытии сокета в отдельном треде
			Task.Run(() => {
				while (isConnected) {
					if (!checkConnection(this.socket, true)) {
						disconnect();
						break;
					}
					if (this.socket.Available == 0) {
						Thread.Sleep(1000);
						continue;
					}

					do {
						bytesRead = this.socket.Receive(buffer);
						data.AddRange(buffer.ToList().GetRange(0, bytesRead));
					} while (this.socket.Available > 0);
					var message = Encoding.UTF8.GetString(data.ToArray());
					this.Dispatcher.Invoke(() => logMessage($"Получено сообщение: <{message}> ", this.socket?.RemoteEndPoint?.ToString()));
					data.Clear();
					if (message == Responses.NACK) {
						disconnect();
						break;
					}
				}
			});
		}

		/// <summary>
		/// Закрытие соединения
		/// </summary>
		private void disconnect() {
			if (checkConnection(this.socket)) {
				this.socket?.Send(Encoding.UTF8.GetBytes(Responses.NACK));
			}
			this.Dispatcher.Invoke(() => logMessage($"Закрытие соединения", this.socket?.RemoteEndPoint?.ToString()));
			this.socket?.Close();
			this.socket = null;
			this.Dispatcher.Invoke(() => updateTextBoxesAvailiability());
			this.Dispatcher.Invoke(() => updateButtonsAvailiability());
		}

		#endregion
	}
}