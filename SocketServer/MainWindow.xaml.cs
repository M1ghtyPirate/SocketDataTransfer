using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

namespace SocketServer {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		#region Properties

		/// <summary>
		/// Параметры для открытия сокета заполнены
		/// </summary>
		private bool parametersFilled { get => !string.IsNullOrWhiteSpace(this.IPAddressTextBox?.Text) && !string.IsNullOrWhiteSpace(this.PortTextBox?.Text) && this.PasswordHashLabel?.Tag != null; }

		/// <summary>
		/// Сокет открыт
		/// </summary>
		private bool isListening { get => this.socket != null; }

		/// <summary>
		/// Сокет для прослушивания
		/// </summary>
		private Socket socket { get; set; }

		/// <summary>
		/// Сокеты соединений
		/// </summary>
		private List<Socket> connectionHandlers { get; set; } = new List<Socket>();

		/// <summary>
		/// Адрес для прослушивания
		/// </summary>
		private IPEndPoint ipEndPoint { get; set; }

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

		private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e) {
			updatePasswordHash();
			updateButtonsAvailiability();
		}

		private void PasswordHashLabel_Loaded(object sender, RoutedEventArgs e) {
			updatePasswordHash();
			updateButtonsAvailiability();
		}

		private void StartButton_Click(object sender, RoutedEventArgs e) {
			startListening();
			updateButtonsAvailiability();
			updateTextBoxesAvailiability();
		}

		private void StopButton_Click(object sender, RoutedEventArgs e) {
			stopListening();
			updateButtonsAvailiability();
			updateTextBoxesAvailiability();
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
		/// Обновление хэша пароля
		/// </summary>
		private void updatePasswordHash() {
			if (this.PasswordTextBox == null || this.PasswordHashLabel == null) {
				return;
			}

			var md5Hash = string.IsNullOrWhiteSpace(this.PasswordTextBox.Text) ? null : MD5.HashData(Encoding.UTF8.GetBytes(this.PasswordTextBox.Text));
			this.PasswordHashLabel.Tag = md5Hash;
			this.PasswordHashLabel.Content = md5Hash == null ? "" : Convert.ToHexString(md5Hash);
		}

		/// <summary>
		/// Обновление доступности кнопок старта и окончания прослушивания
		/// </summary>
		private void updateButtonsAvailiability() {
			if (this.StartButton == null || this.StopButton == null) {
				return;
			}

			this.StartButton.IsEnabled = parametersFilled && !isListening;
			this.StopButton.IsEnabled = isListening;
		}

		/// <summary>
		/// Обновление доступности полей ввода параметров
		/// </summary>
		private void updateTextBoxesAvailiability() {
			if(this.IPAddressTextBox == null || this.PortTextBox == null || this.PasswordTextBox == null) {
				return;
			}

			this.IPAddressTextBox.IsEnabled = this.PortTextBox.IsEnabled = this.PasswordTextBox.IsEnabled = !isListening;
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
				if(logFailure) {
					this.Dispatcher.Invoke(() => logMessage($"Клиент не отвечает", handler?.RemoteEndPoint?.ToString()));
				}
				connected = false;
			}

			handler.Blocking = blockingState;
			return connected;
		}

		/// <summary>
		/// Закрытие подключения
		/// </summary>
		/// <param name="handler"></param>
		private void disconnect(Socket handler) {
			if (checkConnection(handler)) {
				handler?.Send(Encoding.UTF8.GetBytes(Responses.NACK));
			}
			this.Dispatcher.Invoke(() => logMessage($"Закрытие соединения", handler?.RemoteEndPoint?.ToString()));
			this.Dispatcher.Invoke(() => {
				if (connectionHandlers.Contains(handler)) {
					connectionHandlers.Remove(handler);
				}
			});
			handler?.Close();
			
		}

		/// <summary>
		/// Обработка подключения
		/// </summary>
		/// <param name="handler"></param>
		private void handleConnection(Socket handler) {
			if(!isListening) {
				return;
			}

			this.Dispatcher.Invoke(() => logMessage($"Открыто соединение", handler?.RemoteEndPoint?.ToString()));
			var buffer = new byte[256];
			var data = new List<byte>();
			var bytesRead = 0;

			// Получение данных о студенте
			do {
				bytesRead = handler.Receive(buffer);
				data.AddRange(buffer.ToList().GetRange(0, bytesRead));
			} while (handler.Available > 0);
			var studentInfo = Encoding.UTF8.GetString(data.ToArray());
			this.Dispatcher.Invoke(() => logMessage($"Данные о студенте: <{studentInfo}>", handler?.RemoteEndPoint?.ToString()));
			data.Clear();

			handler.Send(Encoding.UTF8.GetBytes(Responses.ACK));

			// Получение хэша данных о студенте
			do {
				bytesRead = handler.Receive(buffer);
				data.AddRange(buffer.ToList().GetRange(0, bytesRead));
			} while (handler.Available > 0);
			int? studentInfoHashValue = (int)studentInfo.First() + (int)studentInfo[studentInfo.Length / 2] + (int)studentInfo.Last();
			var studentInfoHash = Encoding.UTF8.GetBytes(studentInfoHashValue + "");
			var hashesAreEqual = studentInfoHash.SequenceEqual(data);
			this.Dispatcher.Invoke(() => logMessage($"Хэши данных {(hashesAreEqual ? "" : "не ")}равны: получено: <{Convert.ToHexString(data.ToArray())}>; вычислено: <{Convert.ToHexString(studentInfoHash)}> ", handler?.RemoteEndPoint?.ToString()));
			data.Clear();

			handler.Send(Encoding.UTF8.GetBytes(hashesAreEqual ? Responses.ACK : Responses.NACK));
			if (!hashesAreEqual) {
				disconnect(handler);
				return;
			}

			// Получение хэша пароля
			do {
				bytesRead = handler.Receive(buffer);
				data.AddRange(buffer.ToList().GetRange(0, bytesRead));
			} while (handler.Available > 0);
			var passwordHashValue = Encoding.UTF8.GetString(data.ToArray());
			if (passwordHashValue == Responses.NACK) {
				this.Dispatcher.Invoke(() => logMessage($"Получено сообщение: <{passwordHashValue}> ", handler?.RemoteEndPoint?.ToString()));
				disconnect(handler);
				return;
			}
			var passwordHash = this.Dispatcher.Invoke(() => PasswordHashLabel.Tag as byte[]);
			hashesAreEqual = passwordHash.SequenceEqual(data);
			this.Dispatcher.Invoke(() => logMessage($"Хэши паролей {(hashesAreEqual ? "" : "не ")}равны: получено: <{Convert.ToHexString(data.ToArray())}>; вычислено: <{Convert.ToHexString(passwordHash)}> ", handler?.RemoteEndPoint?.ToString()));
			data.Clear();

			handler.Send(Encoding.UTF8.GetBytes(hashesAreEqual ? Responses.ACK : Responses.NACK));
			if (!hashesAreEqual) {
				disconnect(handler);
				return;
			}

			// Ожидание сообщений
			while(isListening) {
				if (!checkConnection(handler, true)) {
					disconnect(handler);
					break;
				}
				if (handler.Available == 0) {
					Thread.Sleep(1000);
					continue;
				}

				do {
					bytesRead = handler.Receive(buffer);
					data.AddRange(buffer.ToList().GetRange(0, bytesRead));
				} while (handler.Available > 0);
				var message = Encoding.UTF8.GetString(data.ToArray());
				this.Dispatcher.Invoke(() => logMessage($"Получено сообщение: <{message}> ", handler?.RemoteEndPoint?.ToString()));
				data.Clear();
				if (message == Responses.NACK) {
					disconnect(handler);
					break;
				}
				handler.Send(Encoding.UTF8.GetBytes(Responses.ACK));
			}
		}

		/// <summary>
		/// Старт прослушивания
		/// </summary>
		private void startListening() {
			if (!parametersFilled) {
				logMessage("Не заполнены параметры.");
				return;
			}

			if (!IPAddress.TryParse(this.IPAddressTextBox.Text, out var ip) || !int.TryParse(this.PortTextBox.Text, out var port)) {
				logMessage($"Не удалось сформировать адрес для открытия сокета: <{this.IPAddressTextBox.Text}>:<{this.PortTextBox.Text}>");
				return;
			}

			this.ipEndPoint = new IPEndPoint(ip, port);
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.socket.Bind(this.ipEndPoint);
			this.socket.Listen(10);
			logMessage($"Сокет начал прослушивание", this.socket?.LocalEndPoint?.ToString());
			Task.Run(() => {
				while (isListening) {
					var handler = this.socket.Accept();
					this.Dispatcher.Invoke(() => this.connectionHandlers.Add(handler));
					Task.Run(() => handleConnection(handler));
				}
			});
			
		}

		/// <summary>
		/// Окончание прослушивания
		/// </summary>
		private void stopListening() {
			foreach (var handler in connectionHandlers.ToArray()) {
				disconnect(handler);
			}
			connectionHandlers.Clear();
			var currentSocket = this.socket;

			// Разлочим сокет
			this.socket = null;
			var localConnection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			localConnection.Connect(this.ipEndPoint);
			localConnection.Close();

			currentSocket.Close();
			this.ipEndPoint = null;
			this.Dispatcher.Invoke(() => logMessage($"Сокет закончил прослушивание", this.socket?.LocalEndPoint?.ToString()));
		}

		#endregion
	}
}