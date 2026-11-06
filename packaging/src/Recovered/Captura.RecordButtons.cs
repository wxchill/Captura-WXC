using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.0.0.0")]
namespace CapturaRecordControls
{
	public static class RecordButtonSplitter
	{
		private const string MarkerKey = "Captura.Local.RecordSplitDone";

		public static void Install(object instance)
		{
			try
			{
				Window window = instance as Window;
				if (window != null && !window.Resources.Contains("Captura.Local.RecordSplitDone"))
				{
					window.Resources["Captura.Local.RecordSplitDone"] = true;
					window.Loaded += delegate
					{
						Split(window);
					};
				}
			}
			catch
			{
			}
		}

		private static object Prop(object o, string name)
		{
			if (o == null)
			{
				return null;
			}
			PropertyInfo property = o.GetType().GetProperty(name);
			if (!(property == null))
			{
				return property.GetValue(o, null);
			}
			return null;
		}

		private static Assembly FindAss(string simpleName)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				if (string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
				{
					return assembly;
				}
			}
			return null;
		}

		private static void Split(Window window)
		{
			try
			{
				object recorder = Prop(window.DataContext, "RecordingViewModel");
				object obj = Prop(recorder, "RecordCommand");
				if (recorder == null || obj == null)
				{
					return;
				}
				ButtonBase buttonBase = FindButtonByCommand(window, obj);
				if (buttonBase == null)
				{
					return;
				}
				Panel panel = buttonBase.Parent as Panel;
				if (panel == null)
				{
					return;
				}
				int num = panel.Children.IndexOf(buttonBase);
				Geometry record;
				Geometry stop;
				if (num < 0 || !TryGetIcons(window.GetType().Assembly, out record, out stop))
				{
					return;
				}
				Brush brush = buttonBase.Foreground;
				if (brush == null)
				{
					brush = new SolidColorBrush(Color.FromRgb(183, 28, 28));
				}
				ButtonBase buttonBase2 = MakeButton(window.GetType().Assembly, record, brush, "录制");
				ButtonBase buttonBase3 = MakeButton(window.GetType().Assembly, stop, brush, "停止");
				if (buttonBase2 == null || buttonBase3 == null)
				{
					return;
				}
				buttonBase2.Click += delegate
				{
					if (GetState(recorder) == "NotRecording")
					{
						StartRecord(recorder);
					}
				};
				buttonBase3.Click += delegate
				{
					string state = GetState(recorder);
					if (state == "Recording" || state == "Paused")
					{
						StopRecord(recorder);
					}
				};
				SubscribeState(recorder, buttonBase2, buttonBase3);
				panel.Children.RemoveAt(num);
				panel.Children.Insert(num, buttonBase2);
				panel.Children.Insert(num + 1, buttonBase3);
				ApplyEnabled(recorder, buttonBase2, buttonBase3);
			}
			catch
			{
			}
		}

		private static ButtonBase MakeButton(Assembly captura, Geometry icon, Brush foreground, string name)
		{
			Type type = captura.GetType("Captura.ModernButton");
			if (type == null)
			{
				return null;
			}
			object obj;
			try
			{
				obj = Activator.CreateInstance(type);
			}
			catch
			{
				return null;
			}
			ButtonBase result = (ButtonBase)obj;
			Control control = (Control)obj;
			FrameworkElement frameworkElement = (FrameworkElement)obj;
			control.Foreground = foreground;
			frameworkElement.ToolTip = name;
			AutomationProperties.SetName(frameworkElement, name);
			FieldInfo field = type.GetField("IconDataProperty", BindingFlags.Static | BindingFlags.Public);
			if (field != null)
			{
				DependencyProperty dependencyProperty = field.GetValue(null) as DependencyProperty;
				if (dependencyProperty != null)
				{
					((DependencyObject)obj).SetValue(dependencyProperty, icon);
				}
			}
			return result;
		}

		private static bool TryGetIcons(Assembly captura, out Geometry record, out Geometry stop)
		{
			record = null;
			stop = null;
			try
			{
				Type type = captura.GetType("Captura.StateToRecordButtonGeometryConverter");
				if (type == null)
				{
					return false;
				}
				object obj = Activator.CreateInstance(type);
				Type type2 = null;
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly assembly in assemblies)
				{
					Type type3 = assembly.GetType("Captura.Models.RecorderState");
					if (type3 != null)
					{
						type2 = type3;
						break;
					}
				}
				if (type2 == null)
				{
					return false;
				}
				MethodInfo method = type.GetMethod("Convert");
				object obj2 = Enum.Parse(type2, "NotRecording");
				object obj3 = Enum.Parse(type2, "Recording");
				record = method.Invoke(obj, new object[4]
				{
					obj2,
					typeof(Geometry),
					null,
					CultureInfo.InvariantCulture
				}) as Geometry;
				stop = method.Invoke(obj, new object[4]
				{
					obj3,
					typeof(Geometry),
					null,
					CultureInfo.InvariantCulture
				}) as Geometry;
				return record != null && stop != null;
			}
			catch
			{
				return false;
			}
		}

		private static ButtonBase FindButtonByCommand(Window window, object command)
		{
			foreach (ButtonBase item in EnumerateDescendants<ButtonBase>(window))
			{
				if (item.Command != null && object.ReferenceEquals(item.Command, command))
				{
					return item;
				}
			}
			return null;
		}

		private static IEnumerable<T> EnumerateDescendants<T>(DependencyObject root) where T : DependencyObject
		{
			int count = VisualTreeHelper.GetChildrenCount(root);
			for (int i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				if (child is T)
				{
					yield return (T)child;
				}
				foreach (T item in EnumerateDescendants<T>(child))
				{
					yield return item;
				}
			}
		}

		private static string GetState(object recorder)
		{
			object obj = Prop(recorder, "RecorderState");
			if (obj != null)
			{
				return obj.ToString();
			}
			return null;
		}

		private static void StartRecord(object recorder)
		{
			try
			{
				MethodInfo method = recorder.GetType().GetMethod("StartRecording");
				if (method != null)
				{
					object[] parameters = new object[1];
					method.Invoke(recorder, parameters);
				}
			}
			catch
			{
			}
		}

		private static void StopRecord(object recorder)
		{
			try
			{
				MethodInfo method = recorder.GetType().GetMethod("StopRecording");
				if (method != null)
				{
					method.Invoke(recorder, null);
				}
			}
			catch
			{
			}
		}

		private static void ApplyEnabled(object recorder, ButtonBase rec, ButtonBase stop)
		{
			string state = GetState(recorder);
			rec.IsEnabled = state == "NotRecording";
			stop.IsEnabled = state == "Recording" || state == "Paused";
		}

		private static void SubscribeState(object recorder, ButtonBase rec, ButtonBase stop)
		{
			INotifyPropertyChanged notifyPropertyChanged = recorder as INotifyPropertyChanged;
			if (notifyPropertyChanged == null)
			{
				return;
			}
			PropertyChangedEventHandler propertyChangedEventHandler = null;
			propertyChangedEventHandler = delegate(object sender, PropertyChangedEventArgs e)
			{
				if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "RecorderState")
				{
					ApplyEnabled(recorder, rec, stop);
				}
			};
			notifyPropertyChanged.PropertyChanged += propertyChangedEventHandler;
			rec.Tag = new object[2] { notifyPropertyChanged, propertyChangedEventHandler };
		}
	}
}
