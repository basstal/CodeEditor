using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEngine;

namespace CodeEditor
{
    
    public static class Utility
    {
        public class ShellResult
        {
            public int exitCode { get; }
            public string stdOut { get; }
            public string stdErr { get; }

            public ShellResult(int exitCode, string stdOut, string stdErr)
            {
                this.exitCode = exitCode;
                this.stdOut = stdOut;
                this.stdErr = stdErr;
            }
        }

        public static ShellResult Shell(string executable, string[] args, string workingDir = null, bool waitForExit = true, Dictionary<string, string> environment = null)
        {
            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = executable;
                process.StartInfo.Arguments = String.Join(" ", args);
                process.StartInfo.WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory();

                if (environment != null)
                {
                    foreach (var entry in environment)
                    {
                        process.StartInfo.EnvironmentVariables[entry.Key] = entry.Value;
                    }
                }

                process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                StringBuilder stdoutsb = new StringBuilder();
                process.OutputDataReceived += (obj, sender) => { stdoutsb.Append("\n" + sender.Data); };

                process.StartInfo.RedirectStandardError = true;
                StringBuilder stderrsb = new StringBuilder();
                process.ErrorDataReceived += (obj, sender) => { stderrsb.Append("\n" + sender.Data); };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (waitForExit)
                {
                    process.WaitForExit();
                    return new ShellResult(process.ExitCode, stdoutsb.ToString(), stderrsb.ToString());
                }
                else
                {
                    return new ShellResult(0, "", "");
                }
            }
            catch (Exception e)
            {
                return new ShellResult(-1, "", e.Message);
            }
        }

        public static ShellResult ShellWithError(string cmd, string[] args, string workingDir = null, bool waitForExit = true, Dictionary<string, string> environment = null)
        {
            var result = Shell(cmd, args, workingDir, waitForExit, environment);
            if (result.exitCode != 0)
            {
                Debug.LogError($"Shell failed: {result.stdErr}");
            }
            else
            {
                Debug.Log($"Shell successed: {result.stdOut}");

            }

            return result;
        }
        public static object GetIMessageField(IMessage msg, string fieldName)
        {
            var msgType = msg.GetType();
            var prop = msgType.GetProperty(fieldName);
            return prop != null ? prop.GetValue(msg) : null;
        }
        public static void SetIMessageField(IMessage msg, string fieldName, object value)
        {
            var msgType = msg.GetType();
            var prop = msgType.GetProperty(fieldName);
            if (prop != null) prop.SetValue(msg, value);
        }
        public static Type GetType(string nameSpaceAndClass, string assemblyName = "Assembly-CSharp")
        {
            var assembly = Assembly.Load(assemblyName);
            return assembly.GetType(nameSpaceAndClass);
        }
        public static List<T> GetRepeatedFields<T>(IMessage msg, string fieldName)
        {
            var field = GetIMessageField(msg, fieldName);
            return GetRepeatedFields<T>(field);
        }
        public static List<T> GetRepeatedFields<T>(object field)
        {
            List<T> result = new List<T>();
            var type = field.GetType();
            var countProperty = type.GetProperty("Count");
            var itemProperty = type.GetProperty("Item");
            if (countProperty != null && itemProperty != null)
            {
                var count = (int)countProperty.GetValue(field);
                for ( int i = 0; i < count; ++i)
                {
                    var val = itemProperty.GetValue(field, new object[] {i});
                    result.Add((T)val);
                }
            }

            return result;
        }
        
        public static void RemoveAtRepeatedField(IMessage msg, string fieldName, int removeIndex = -1)
        {
            var field = GetIMessageField(msg, fieldName);
            RemoveAtRepeatedField(field, removeIndex);
        }

        public static void RemoveAtRepeatedField(object field, int removeIndex = -1)
        {
            var type = field.GetType();
            var removeAtMethod = type.GetMethod("RemoveAt");
            if (removeIndex == -1)
            {
                var countProperty = type.GetProperty("Count");
                if (countProperty != null) removeIndex = (int) countProperty.GetValue(field) - 1;
            }
            if (removeIndex >= 0)
            {
                removeAtMethod?.Invoke(field, new object[]{removeIndex});
            }
        }
        public static void Insert2RepeatedField(IMessage msg, string fieldName, object value, int index = -1)
        {
            var field = GetIMessageField(msg, fieldName);
            Insert2RepeatedField(field, value, index);
        }

        public static void Insert2RepeatedField(object field, object value, int index = -1)
        {
            var type = field.GetType();
            if (index == -1)
            {
                var countProperty = type.GetProperty("Count");
                if (countProperty != null) index = (int) countProperty.GetValue(field);
            }
            var insertMethod = type.GetMethod("Insert");
            insertMethod?.Invoke(field, new[]{index, value});
        }
        public static Dictionary<string, Type> MessageTypes;

        public static MessageDescriptor GetDescriptor(Type type)
        {
            MessageDescriptor result = null;

            var property = type.GetProperty("Descriptor", BindingFlags.Static | BindingFlags.Public);
            if (property != null)
            {
                result = property.GetValue(null) as MessageDescriptor;
            }

            return result;
        }
        public static IMessage CreateIMessage(string typename)
        {
            if (MessageTypes == null)
            {
                MessageTypes = new Dictionary<string, Type>();
                var baseType = typeof(IMessage);
                var assembly = Assembly.Load("Assembly-CSharp");
                foreach (var messageType in assembly.GetTypes())
                {
                    if (baseType.IsAssignableFrom(messageType))
                    {
                        var descriptor = GetDescriptor(messageType);
                        if (descriptor != null)
                        {
                            MessageTypes.Add(descriptor.FullName, messageType);
                        }
                    }
                }
            }

            MessageTypes.TryGetValue(typename, out var type);        

            return type != null ? Activator.CreateInstance(type) as IMessage : null;
        }
        public const int DECIMAL_BIT = 32;
        public const long DECIMAL_MASK = 0x00000000FFFFFFFFL;//0xFFFFFFFFFFFFFFFF << DECIMAL_BIT >> DECIMAL_BIT;
        public static float FixedToFloat(long number)=> (number & DECIMAL_MASK) / (float)(1L << DECIMAL_BIT)   + (number >> DECIMAL_BIT);
        public static long FloatToFixed(float number)
        {
            long integer = (long)number;
            float decimals = number - integer;
            return (long)(decimals * (1L << DECIMAL_BIT)) + (integer << DECIMAL_BIT);
        }

    }
}
