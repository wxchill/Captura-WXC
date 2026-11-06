using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("1.0.0.0")]
namespace CapturaStatus
{
	public static class RecordingIndicator
	{
		public const string ResourceKey = "Captura.Local.RecordingStatusBanner";

		internal static string overrideText;

		internal static byte overrideR;

		internal static byte overrideG;

		internal static byte overrideB;

		public static void SetSaveOutcome(string kind)
		{
			switch (kind)
			{
			case "saving":
				overrideText = "保存中…";
				overrideR = 29;
				overrideG = 100;
				overrideB = 170;
				break;
			case "saved":
				overrideText = "✓  保存成功  ·  未在录制";
				overrideR = 30;
				overrideG = 133;
				overrideB = 73;
				break;
			case "deleted":
				overrideText = "✓  删除成功  ·  未在录制";
				overrideR = 30;
				overrideG = 133;
				overrideB = 73;
				break;
			case "failed":
				overrideText = "另存失败 · 原录像已保留";
				overrideR = 176;
				overrideG = 64;
				overrideB = 44;
				break;
			default:
				overrideText = null;
				overrideR = (overrideG = (overrideB = 0));
				break;
			}
		}

		public static void Install(object instance)
		{
			try
			{
				Window window = instance as Window;
				if (window != null && !window.Resources.Contains("Captura.Local.RecordingStatusBanner"))
				{
					StatusBanner statusBanner = new StatusBanner(window);
					window.Resources["Captura.Local.RecordingStatusBanner"] = statusBanner;
					statusBanner.Attach();
				}
			}
			catch
			{
			}
		}
	}
	public sealed class StatusBanner : Border
	{
		private readonly Window window;

		private readonly string originalTitle;

		private readonly TextBlock label;

		private object recorder;

		private INotifyPropertyChanged notificationSource;

		private readonly DispatcherTimer refreshTimer;

		private bool resized;

		private bool recordingObserved;

		public string StatusText
		{
			get
			{
				return label.Text;
			}
		}

		public StatusBanner(Window owner)
		{
			window = owner;
			originalTitle = (string.IsNullOrEmpty(owner.Title) ? "Captura" : owner.Title);
			base.Height = 38.0;
			base.Padding = new Thickness(12.0, 6.0, 12.0, 6.0);
			base.IsHitTestVisible = false;
			label = new TextBlock
			{
				FontFamily = new FontFamily("Microsoft YaHei"),
				FontSize = 16.0,
				FontWeight = FontWeights.Bold,
				Foreground = Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				TextTrimming = TextTrimming.CharacterEllipsis
			};
			Child = label;
			refreshTimer = new DispatcherTimer(DispatcherPriority.Background, owner.Dispatcher);
			refreshTimer.Interval = TimeSpan.FromMilliseconds(250.0);
			refreshTimer.Tick += delegate
			{
				Refresh();
			};
			AutomationPropertiesSetName();
		}

		private void AutomationPropertiesSetName()
		{
			AutomationProperties.SetName(this, "录制状态提示条");
		}

		public void Attach()
		{
			UIElement uIElement = window.Content as UIElement;
			if (uIElement == null)
			{
				return;
			}
			DockPanel dockPanel = new DockPanel();
			dockPanel.LastChildFill = true;
			DockPanel dockPanel2 = dockPanel;
			DockPanel.SetDock(this, Dock.Top);
			window.Content = null;
			try
			{
				dockPanel2.Children.Add(this);
				dockPanel2.Children.Add(uIElement);
				window.Content = dockPanel2;
			}
			catch
			{
				dockPanel2.Children.Remove(uIElement);
				window.Content = uIElement;
				throw;
			}
			window.DataContextChanged += delegate
			{
				Connect();
			};
			window.Loaded += delegate
			{
				Connect();
				if (!resized && (window.SizeToContent & SizeToContent.Height) == 0)
				{
					resized = true;
					double num = (double.IsNaN(window.Height) ? window.ActualHeight : window.Height);
					if (num > 0.0 && window.WindowState == WindowState.Normal)
					{
						window.Height = Math.Min(num + base.Height, SystemParameters.WorkArea.Height - 12.0);
					}
				}
			};
			window.Closed += delegate
			{
				refreshTimer.Stop();
				Disconnect();
			};
			Connect();
			refreshTimer.Start();
		}

		private void Disconnect()
		{
			if (notificationSource != null)
			{
				notificationSource.PropertyChanged -= OnChanged;
			}
			notificationSource = null;
		}

		private void Connect()
		{
			Disconnect();
			recorder = Get(window.DataContext, "RecordingViewModel");
			recordingObserved = false;
			notificationSource = recorder as INotifyPropertyChanged;
			if (notificationSource != null)
			{
				notificationSource.PropertyChanged += OnChanged;
			}
			Refresh();
		}

		private static object Get(object value, string name)
		{
			if (value == null)
			{
				return null;
			}
			PropertyInfo property = value.GetType().GetProperty(name);
			if (!(property == null))
			{
				return property.GetValue(value, null);
			}
			return null;
		}

		private object FindCurrentFile()
		{
			if (recorder == null)
			{
				return null;
			}
			FieldInfo field = recorder.GetType().GetField("_currentFileName", BindingFlags.Instance | BindingFlags.NonPublic);
			string text = ((field == null) ? null : (field.GetValue(recorder) as string));
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			object value = Get(window.DataContext, "RecentViewModel");
			IEnumerable enumerable = Get(value, "RecentList") as IEnumerable;
			if (enumerable == null)
			{
				return null;
			}
			foreach (object item in enumerable)
			{
				if (string.Equals(Get(item, "FileName") as string, text, StringComparison.OrdinalIgnoreCase))
				{
					return item;
				}
			}
			return null;
		}

		private void OnChanged(object sender, PropertyChangedEventArgs e)
		{
			if ((string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "RecorderState" || e.PropertyName == "TimeSpan" || e.PropertyName == "Countdown" || e.PropertyName == "RunningStopRecordingCount") && !window.Dispatcher.HasShutdownStarted)
			{
				if (window.Dispatcher.CheckAccess())
				{
					Refresh();
				}
				else
				{
					window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Refresh));
				}
			}
		}

		private void Display(string message, byte r, byte g, byte b, string title)
		{
			if (!(label.Text == message) || !(window.Title == title))
			{
				label.Text = message;
				base.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
				window.Title = title;
				AutomationProperties.SetName(label, message);
			}
		}

		private void Refresh()
		{
			try
			{
				object obj = Get(recorder, "RecorderState");
				if (obj == null)
				{
					Display("状态读取中…", 110, 110, 110, originalTitle);
					return;
				}
				string text = obj.ToString();
				object obj2 = Get(recorder, "TimeSpan");
				string text2 = ((obj2 is TimeSpan) ? ((TimeSpan)obj2).ToString("hh\\:mm\\:ss") : "");
				object obj3 = Get(recorder, "Countdown");
				int num = ((obj3 is int) ? ((int)obj3) : 0);
				object obj4 = Get(recorder, "RunningStopRecordingCount");
				int num2 = ((obj4 is int) ? ((int)obj4) : 0);
				if (text == "Paused")
				{
					recordingObserved = true;
					Display("❚❚  已暂停  " + text2, 192, 111, 0, "【已暂停】" + originalTitle);
				}
				else if (text == "Recording" && num > 0)
				{
					Display("准备录制  倒计时 " + num, 192, 111, 0, "【倒计时】" + originalTitle);
				}
				else if (text == "Recording")
				{
					recordingObserved = true;
					Display("●  正在录制  " + text2, 210, 24, 37, "【正在录制】" + originalTitle);
				}
				else if (text == "NotRecording")
				{
					if (!string.IsNullOrEmpty(RecordingIndicator.overrideText))
					{
						Display(RecordingIndicator.overrideText, RecordingIndicator.overrideR, RecordingIndicator.overrideG, RecordingIndicator.overrideB, originalTitle);
						return;
					}
					object value = FindCurrentFile();
					object obj5 = Get(value, "IsSaving");
					if (obj5 is bool && !(bool)obj5)
					{
						Display("✓  保存成功  ·  未在录制", 30, 133, 73, "【保存成功】" + originalTitle);
					}
					else if (num2 > 0)
					{
						Display("正在保存录像…", 29, 100, 170, "【正在保存】" + originalTitle);
					}
					else if (obj5 is bool && (bool)obj5)
					{
						Display("保存未确认，请检查录像", 192, 111, 0, originalTitle);
					}
					else if (recordingObserved)
					{
						Display("录制已结束  ·  未在录制", 88, 96, 105, originalTitle);
					}
					else
					{
						Display("○  未在录制", 88, 96, 105, originalTitle);
					}
				}
				else
				{
					Display("录制状态待确认", 192, 111, 0, originalTitle);
				}
			}
			catch
			{
				Display("录制状态待确认", 192, 111, 0, originalTitle);
			}
		}
	}
}
