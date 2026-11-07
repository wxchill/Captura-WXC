using System;
using System.Reflection;
using System.Runtime.CompilerServices;
class JitChecks {
 static int Main(){try{
  string root=AppDomain.CurrentDomain.BaseDirectory;
  var host=Assembly.LoadFrom(System.IO.Path.Combine(root,"captura.exe"));
  RuntimeHelpers.PrepareMethod(host.EntryPoint.MethodHandle);
  RuntimeHelpers.PrepareMethod(host.GetType("Captura.MainWindow",true).GetConstructor(Type.EmptyTypes).MethodHandle);
  foreach(string name in new[]{"Captura.Core","Captura.Base"}){var assembly=Assembly.Load(name);foreach(var type in assembly.GetTypes())foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly)){if(!method.ContainsGenericParameters && !method.IsAbstract && method.GetMethodBody()!=null)RuntimeHelpers.PrepareMethod(method.MethodHandle);}}
  Console.WriteLine("PASS: host entrypoint, main constructor and concrete Core/Base methods JIT-compiled without launching the application.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
