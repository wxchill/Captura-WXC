using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.0.0.0")]
namespace CapturaSaveDialog
{
	public static class SaveDialogInterceptor
	{

		private static bool _installShown;


		private static void Log(string msg)
		{
			try
			{
				string text = string.Format("[{0:HH:mm:ss.fff}] {1}", DateTime.Now, msg);
				System.Diagnostics.Debug.WriteLine(text + Environment.NewLine);
			}
			catch
			{
			}
		}

		private static void SetStatusOutcome(string kind)
		{
			try
			{
				Type type = Type.GetType("CapturaStatus.RecordingIndicator, Captura.RecordingStatus");
				if (type == null)
				{
					Log("  SetStatusOutcome: type not found");
					return;
				}
				MethodInfo method = type.GetMethod("SetSaveOutcome", BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					method.Invoke(null, new object[1] { kind });
				}
				else
				{
					Log("  SetStatusOutcome: method not found");
				}
			}
			catch (Exception ex)
			{
				Log("  SetStatusOutcome FAIL: " + ex.Message);
			}
		}

		public static void Install(object instance)
		{
			try
			{
				Log("Install CALLED. instance type=" + ((instance == null) ? "null" : instance.GetType().FullName));
				if (_installShown)
				{
					Log("Install SKIP (already shown)");
					return;
				}
				_installShown = true;
				Window window = instance as Window;
				Log("  is Window? " + (window != null) + "  App.Current=" + (System.Windows.Application.Current != null));
				Log("  Install OK, hook armed");
			}
			catch (Exception ex)
			{
				Log("Install FATAL: " + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public static bool TryInterceptShowNotification(object tray, object notification)
		{
			try
			{
				Log("TryIntercept CALLED. tray=" + ((tray == null) ? "null" : tray.GetType().Name) + " notif=" + ((notification == null) ? "null" : notification.GetType().Name));
				if (notification == null)
				{
					Log("  bail: null notif");
					return false;
				}
				Type type = notification.GetType();
				if (type.Name != "FileSavedNotification")
				{
					Log("  bail: not FileSavedNotification, it's " + type.Name);
					return false;
				}
				string text = null;
				FieldInfo field = type.GetField("_fileName", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field != null)
				{
					text = field.GetValue(notification) as string;
				}
				if (string.IsNullOrEmpty(text))
				{
					PropertyInfo property = type.GetProperty("SecondaryText");
					if (property != null)
					{
						text = property.GetValue(notification, null) as string;
					}
				}
				Log("  FileName=" + (text ?? "<null>"));
				if (string.IsNullOrEmpty(text))
				{
					Log("  bail: empty path");
					return false;
				}
				if (!File.Exists(text))
				{
					Log("  bail: file does not exist on disk");
					return false;
				}
				VideoMeta videoMeta = GatherMetadata(text);
				Log(string.Concat("  meta gathered. size=", videoMeta.FileSize, " dur=", videoMeta.Duration, " src=", videoMeta.VideoSource));
				SetStatusOutcome("saving");
				SaveDialogResult saveDialogResult = SaveDialog.ShowDialog(text, videoMeta);
				Log("  SaveDialog returned: " + saveDialogResult);
				if (saveDialogResult != SaveDialogResult.Saved)
				{
					Log("  NOT Saved — clearing status");
					SetStatusOutcome("");
					return true;
				}
				string currentSavePath = SaveDialog.CurrentSavePath;
				bool currentTrash = SaveDialog.CurrentTrash;
				Log("  Saved. finalPath=" + currentSavePath + " trashed=" + currentTrash);
				if (currentTrash)
				{
					Log("  Trashed: deleting original file, no toast");
					SetStatusOutcome("deleted");
					DeleteFileToTrash(text);
					return true;
				}
				if (!string.IsNullOrEmpty(currentSavePath) && currentSavePath != text)
				{
					try
					{
						SafeRecordingSave.Move(text, currentSavePath);
						FieldInfo field2 = type.GetField("_fileName", BindingFlags.Instance | BindingFlags.NonPublic);
						if (field2 != null)
						{
							field2.SetValue(notification, currentSavePath);
						}
						text = currentSavePath;
						Log("  File moved/renamed OK");
					}
					catch (Exception ex)
					{
						Log("  Move FAILED: " + ex.Message);
						SetStatusOutcome("failed");
						System.Windows.MessageBox.Show("另存失败，原录像仍保留在：\n" + text + "\n\n" + ex.Message, "Captura", MessageBoxButton.OK, MessageBoxImage.Warning);
						return true;
					}
				}
				SetStatusOutcome("saved");
				Log("  About to run original ShowNotification (toast)");
				RunOriginalShowNotification(tray, notification);
				Log("  Done. Returning true.");
				return true;
			}
			catch (Exception ex2)
			{
				Log("TryIntercept FATAL: " + ex2.GetType().Name + " " + ex2.Message + "\n" + ex2.StackTrace);
				return false;
			}
		}

		private static VideoMeta GatherMetadata(string path)
		{
			VideoMeta videoMeta = new VideoMeta();
			try
			{
				videoMeta.FileSize = new FileInfo(path).Length;
			}
			catch
			{
			}
			object obj2 = FindRecordingViewModel();
			if (obj2 != null)
			{
				try
				{
					PropertyInfo property = obj2.GetType().GetProperty("TimeSpan");
					if (property != null)
					{
						videoMeta.Duration = (TimeSpan)property.GetValue(obj2, null);
					}
				}
				catch
				{
				}
				try
				{
					PropertyInfo property2 = obj2.GetType().GetProperty("VideoViewModel");
					if (property2 != null)
					{
						object value = property2.GetValue(obj2, null);
						if (value != null)
						{
							PropertyInfo property3 = value.GetType().GetProperty("SelectedVideoSourceKind");
							if (property3 != null)
							{
								object value2 = property3.GetValue(value, null);
								if (value2 != null)
								{
									string text = null;
									try
									{
										PropertyInfo property4 = value2.GetType().GetProperty("Source");
										if (property4 != null)
										{
											object value3 = property4.GetValue(value2, null);
											if (value3 != null)
											{
												PropertyInfo property5 = value3.GetType().GetProperty("Name");
												if (property5 != null)
												{
													text = property5.GetValue(value3, null) as string;
												}
											}
										}
									}
									catch
									{
									}
									if (string.IsNullOrEmpty(text))
									{
										try
										{
											PropertyInfo property6 = value2.GetType().GetProperty("Name");
											if (property6 != null)
											{
												text = property6.GetValue(value2, null) as string;
											}
										}
										catch
										{
										}
									}
									if (!string.IsNullOrEmpty(text))
									{
										videoMeta.VideoSource = text;
									}
									else
									{
										videoMeta.VideoSource = value2.ToString();
									}
								}
							}
						}
					}
				}
				catch
				{
				}
			}
			if (string.IsNullOrEmpty(videoMeta.VideoSource))
			{
				videoMeta.VideoSource = "Video";
			}
			return videoMeta;
		}

		private static object FindRecordingViewModel()
		{
			try
			{
				System.Windows.Application current = System.Windows.Application.Current;
				if (current == null)
				{
					return null;
				}
				foreach (object window in current.Windows)
				{
					DependencyObject dependencyObject = window as DependencyObject;
					if (dependencyObject == null)
					{
						continue;
					}
					object value = dependencyObject.GetValue(FrameworkElement.DataContextProperty);
					if (value == null)
					{
						continue;
					}
					PropertyInfo property = value.GetType().GetProperty("RecordingViewModel");
					if (!(property == null))
					{
						object value2 = property.GetValue(value, null);
						if (value2 != null)
						{
							return value2;
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static void RunOriginalShowNotification(object tray, object notification)
		{
			try
			{
				Type type = tray.GetType();
				FieldInfo field = type.GetField("_notificationStack", BindingFlags.Instance | BindingFlags.NonPublic);
				object obj = ((field != null) ? field.GetValue(tray) : null);
				if (obj == null)
				{
					return;
				}
				Assembly assembly = type.Assembly;
				Type type2 = assembly.GetType("Captura.NotificationBalloon");
				if (type2 == null)
				{
					return;
				}
				ConstructorInfo constructorInfo = type2.GetConstructors().FirstOrDefault((ConstructorInfo c) => c.GetParameters().Length == 1);
				if (!(constructorInfo == null))
				{
					object obj2 = constructorInfo.Invoke(new object[1] { notification });
					MethodInfo method = obj.GetType().GetMethod("Add");
					if (method != null)
					{
						method.Invoke(obj, new object[1] { obj2 });
					}
					MethodInfo method2 = type.GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
					if (method2 != null)
					{
						method2.Invoke(tray, null);
					}
				}
			}
			catch
			{
			}
		}

		public static void DeleteFileToTrash(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					ShellFileOperation.FileOperation(FileOperationFlags.FO_DELETE, path);
				}
			}
			catch
			{
				try
				{
					File.Delete(path);
				}
				catch
				{
				}
			}
		}
	}
	public class VideoMeta
	{
		public long FileSize;

		public TimeSpan Duration;

		public string VideoSource;
	}
	public enum SaveDialogResult
	{
		None,
		Saved,
		Trash
	}
	public static class SaveDialog
	{
		public static string CurrentSavePath;

		public static bool CurrentTrash;

		public static SaveDialogResult ShowDialog(string originalPath, VideoMeta meta)
		{
			try
			{
				System.Diagnostics.Debug.WriteLine("[DIALOG] ShowDialog ENTER. path=" + originalPath + "\n");
			}
			catch
			{
			}
			Window win = new Window
			{
				Title = "保存录制",
				Width = 520.0,
				Height = 500.0,
				WindowStartupLocation = WindowStartupLocation.CenterScreen,
				ResizeMode = ResizeMode.NoResize,
				Topmost = true,
				Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)),
				Foreground = new SolidColorBrush(Colors.White),
				FontFamily = new FontFamily("Microsoft YaHei UI")
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.Margin = new Thickness(16.0);
			StackPanel stackPanel2 = stackPanel;
			Border border = new Border();
			border.Background = new SolidColorBrush(Color.FromRgb(55, 55, 61));
			border.CornerRadius = new CornerRadius(6.0);
			border.Padding = new Thickness(12.0);
			border.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
			Border border2 = border;
			StackPanel stackPanel3 = new StackPanel();
			stackPanel3.Children.Add(MakeInfoRow("时长：", FormatDuration(meta.Duration), 220));
			stackPanel3.Children.Add(MakeInfoRow("大小：", FormatSize(meta.FileSize), 220));
			stackPanel3.Children.Add(MakeInfoRow("视频源：", meta.VideoSource, 220));
			border2.Child = stackPanel3;
			stackPanel2.Children.Add(border2);
			stackPanel2.Children.Add(new TextBlock
			{
				Text = "保存路径：",
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0),
				Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204))
			});
			Border border3 = new Border();
			border3.Background = new SolidColorBrush(Color.FromRgb(55, 55, 61));
			border3.CornerRadius = new CornerRadius(4.0);
			border3.Padding = new Thickness(8.0);
			border3.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
			Border border4 = border3;
			TextBlock textBlock = new TextBlock();
			textBlock.Text = originalPath;
			textBlock.Foreground = new SolidColorBrush(Colors.White);
			textBlock.TextWrapping = TextWrapping.Wrap;
			TextBlock child = textBlock;
			border4.Child = child;
			stackPanel2.Children.Add(border4);
			stackPanel2.Children.Add(new TextBlock
			{
				Text = "文件名：",
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0),
				Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204))
			});
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(100.0)
			});
			System.Windows.Controls.TextBox fileNameBox = new System.Windows.Controls.TextBox
			{
				Text = Path.GetFileNameWithoutExtension(originalPath),
				Margin = new Thickness(0.0, 0.0, 8.0, 12.0),
				Padding = new Thickness(6.0),
				Background = new SolidColorBrush(Color.FromRgb(64, 64, 72)),
				Foreground = new SolidColorBrush(Colors.White),
				BorderThickness = new Thickness(0.0)
			};
			System.Windows.Controls.TextBox extBox = new System.Windows.Controls.TextBox
			{
				Text = Path.GetExtension(originalPath),
				Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
				Padding = new Thickness(6.0),
				Background = new SolidColorBrush(Color.FromRgb(64, 64, 72)),
				Foreground = new SolidColorBrush(Colors.White),
				BorderThickness = new Thickness(0.0),
				ToolTip = "文件扩展名（如 .mp4）"
			};
			RoutedEventHandler selHandler = null;
			selHandler = delegate
			{
				fileNameBox.Loaded -= selHandler;
				fileNameBox.Focus();
				fileNameBox.SelectAll();
			};
			fileNameBox.Loaded += selHandler;
			Grid.SetColumn(fileNameBox, 0);
			Grid.SetColumn(extBox, 1);
			grid.Children.Add(fileNameBox);
			grid.Children.Add(extBox);
			stackPanel2.Children.Add(grid);
			stackPanel2.Children.Add(new TextBlock
			{
				Text = "目录：",
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0),
				Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204))
			});
			Grid grid2 = new Grid();
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(80.0)
			});
			System.Windows.Controls.TextBox dirBox = new System.Windows.Controls.TextBox
			{
				Text = Path.GetDirectoryName(originalPath),
				Margin = new Thickness(0.0, 0.0, 8.0, 12.0),
				Padding = new Thickness(6.0),
				Background = new SolidColorBrush(Color.FromRgb(64, 64, 72)),
				Foreground = new SolidColorBrush(Colors.White),
				BorderThickness = new Thickness(0.0)
			};
			System.Windows.Controls.Button button = new System.Windows.Controls.Button();
			button.Content = "浏览...";
			button.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
			button.Background = new SolidColorBrush(Color.FromRgb(64, 64, 72));
			button.Foreground = new SolidColorBrush(Colors.White);
			button.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 93));
			button.Padding = new Thickness(8.0, 4.0, 8.0, 4.0);
			button.Cursor = System.Windows.Input.Cursors.Hand;
			System.Windows.Controls.Button button2 = button;
			button2.Click += delegate
			{
				FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
				{
					ShowNewFolderButton = true,
					SelectedPath = dirBox.Text
				};
				if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
				{
					dirBox.Text = folderBrowserDialog.SelectedPath;
				}
			};
			Grid.SetColumn(dirBox, 0);
			Grid.SetColumn(button2, 1);
			grid2.Children.Add(dirBox);
			grid2.Children.Add(button2);
			stackPanel2.Children.Add(grid2);
			StackPanel stackPanel4 = new StackPanel();
			stackPanel4.Orientation = System.Windows.Controls.Orientation.Horizontal;
			stackPanel4.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			StackPanel stackPanel5 = stackPanel4;
			System.Windows.Controls.Button button3 = new System.Windows.Controls.Button();
			button3.Content = "删除";
			button3.Width = 90.0;
			button3.Height = 30.0;
			button3.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			button3.Background = new SolidColorBrush(Color.FromRgb(196, 43, 28));
			button3.Foreground = new SolidColorBrush(Colors.White);
			button3.BorderThickness = new Thickness(0.0);
			button3.Cursor = System.Windows.Input.Cursors.Hand;
			System.Windows.Controls.Button button4 = button3;
			System.Windows.Controls.Button button5 = new System.Windows.Controls.Button();
			button5.Content = "保存";
			button5.Width = 90.0;
			button5.Height = 30.0;
			button5.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
			button5.Foreground = new SolidColorBrush(Colors.White);
			button5.BorderThickness = new Thickness(0.0);
			button5.Cursor = System.Windows.Input.Cursors.Hand;
			button5.IsDefault = true;
			System.Windows.Controls.Button button6 = button5;
			button4.Click += delegate
			{
				CurrentTrash = true;
				CurrentSavePath = "";
				win.DialogResult = true;
				win.Close();
			};
			button6.Click += delegate
			{
				string text = (dirBox.Text ?? "").Trim();
				string text2 = (fileNameBox.Text ?? "").Trim();
				string text3 = (extBox.Text ?? "").Trim();
				if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text2))
				{
					System.Windows.MessageBox.Show("文件名和目录不能为空。", "Captura", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					if (string.IsNullOrEmpty(text3))
					{
						text3 = Path.GetExtension(originalPath);
					}
					if (!text3.StartsWith("."))
					{
						text3 = "." + text3;
					}
					string target;
					try {
						if (text2.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || text3.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
							throw new ArgumentException("文件名或扩展名包含无效字符。");
						target = Path.GetFullPath(Path.Combine(text, text2 + text3));
					} catch (Exception e) {
						System.Windows.MessageBox.Show(e.Message, "Captura", MessageBoxButton.OK, MessageBoxImage.Warning);
						return;
					}
					if (!String.Equals(target, Path.GetFullPath(originalPath), StringComparison.OrdinalIgnoreCase) && File.Exists(target) &&
						System.Windows.MessageBox.Show("目标文件已存在，是否替换？\n" + target, "Captura", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
					CurrentSavePath = target;
					CurrentTrash = false;
					win.DialogResult = true;
					win.Close();
				}
			};
			stackPanel5.Children.Add(button6);
			stackPanel5.Children.Add(button4);
			stackPanel2.Children.Add(stackPanel5);
			win.Content = stackPanel2;
			try
			{
				System.Diagnostics.Debug.WriteLine("[DIALOG] win.ShowDialog() about to call\n");
			}
			catch
			{
			}
			win.ShowDialog();
			try
			{
				System.Diagnostics.Debug.WriteLine("[DIALOG] win.ShowDialog() returned. DialogResult=" + win.DialogResult + "\n");
			}
			catch
			{
			}
			if (win.DialogResult != true)
			{
				return SaveDialogResult.None;
			}
			return SaveDialogResult.Saved;
		}

		private static UIElement MakeInfoRow(string label, string value, int valueMaxWidth)
		{
			if (valueMaxWidth <= 0)
			{
				valueMaxWidth = 220;
			}
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(90.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			TextBlock textBlock = new TextBlock();
			textBlock.Text = label;
			textBlock.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 168));
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			TextBlock element = textBlock;
			TextBlock textBlock2 = new TextBlock();
			textBlock2.Text = value;
			textBlock2.Foreground = new SolidColorBrush(Colors.White);
			textBlock2.VerticalAlignment = VerticalAlignment.Center;
			textBlock2.TextTrimming = TextTrimming.CharacterEllipsis;
			textBlock2.TextWrapping = TextWrapping.NoWrap;
			textBlock2.MaxWidth = valueMaxWidth;
			TextBlock element2 = textBlock2;
			ToolTipService.SetToolTip(element2, value);
			Grid.SetColumn(element, 0);
			Grid.SetColumn(element2, 1);
			grid.Children.Add(element);
			grid.Children.Add(element2);
			return grid;
		}

		private static string FormatDuration(TimeSpan t)
		{
			if (t.TotalSeconds <= 0.0)
			{
				return "--";
			}
			return t.ToString("hh\\:mm\\:ss");
		}

		private static string FormatSize(long bytes)
		{
			if (bytes <= 0)
			{
				return "--";
			}
			string[] array = new string[4] { "B", "KB", "MB", "GB" };
			double num = bytes;
			int num2 = 0;
			while (num >= 1024.0 && num2 < array.Length - 1)
			{
				num /= 1024.0;
				num2++;
			}
			return string.Format("{0:0.##} {1}", num, array[num2]);
		}
	}
	[Flags]
	internal enum FileOperationFlags : uint
	{
		FO_MOVE = 1u,
		FO_COPY = 2u,
		FO_DELETE = 3u,
		FO_RENAME = 4u,
		FOF_MULTIDESTFILES = 1u,
		FOF_SILENT = 4u,
		FOF_NOCONFIRMATION = 0x10u,
		FOF_ALLOWUNDO = 0x40u,
		FOF_NOERRORUI = 0x400u
	}
	internal static class ShellFileOperation
	{
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct SHFILEOPSTRUCT
		{
			public IntPtr hwnd;

			public FileOperationFlags wFunc;

			public string pFrom;

			public string pTo;

			public FileOperationFlags fFlags;

			public bool fAnyOperationsAborted;

			public IntPtr hNameMappings;

			public string lpszProgressTitle;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

		public static int FileOperation(FileOperationFlags operation, string path)
		{
			SHFILEOPSTRUCT FileOp = new SHFILEOPSTRUCT
			{
				wFunc = operation,
				pFrom = path + "\0",
				fFlags = (FileOperationFlags.FO_RENAME | FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_ALLOWUNDO | FileOperationFlags.FOF_NOERRORUI)
			};
			return SHFileOperation(ref FileOp);
		}
	}
}
