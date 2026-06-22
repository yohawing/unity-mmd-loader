#nullable enable

using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Mmd.Samples.RuntimeVerification
{
    public sealed class MmdRuntimeVerificationBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            MmdRuntimeVerificationArguments arguments =
                MmdRuntimeVerificationArguments.Parse(Environment.GetCommandLineArgs());
            var runner = new MmdRuntimeVerificationRunner(arguments);
            yield return runner.Run();

            string json = JsonUtility.ToJson(runner.Report, prettyPrint: true);
            if (!string.IsNullOrWhiteSpace(arguments.OutputPath))
            {
                string? directory = Path.GetDirectoryName(arguments.OutputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(arguments.OutputPath, json);
            }

            Console.WriteLine(json);
            Debug.Log(json);
            Application.Quit(runner.Report.exitCode);
        }
    }
}
