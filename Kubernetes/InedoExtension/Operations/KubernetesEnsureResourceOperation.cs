using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Kubernetes.Configurations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Extensions.Kubernetes.Operations
{
    [DisplayName("Ensure Kubernetes Resource")]
    [Description("Ensures a resource is configured in Kubernetes.")]
    [ScriptAlias("Ensure-Resource")]
    [Tag("kubernetes")]
    public sealed class KubernetesEnsureResourceOperation : EnsureOperation<KubernetesResourceConfiguration>
    {
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var config = new KubernetesResourceConfiguration
            {
                ResourceType = this.Template.ResourceType,
                Namespace = this.Template.Namespace,
                Name = this.Template.Name,
                Exists = false
            };

            using (var sw = new StringWriter())
            {
                await this.RunKubeCtlAsync(context, "apply", new[]
                {
                    "--dry-run",
                    "--output",
                    "json"
                }, sw.WriteLine);

                config.NormalizedApplied = sw.ToString();
            }

            using (var sw = new StringWriter())
            {
                await this.RunKubeCtlAsync(context, "create", new[]
                {
                    "--dry-run",
                    "--output",
                    "json"
                }, sw.WriteLine);

                config.NormalizedTemplate = sw.ToString();
            }

            var body = await this.GetCurrentConfigurationAsync(context, "json");
            if (!string.IsNullOrWhiteSpace(body))
            {
                config.NormalizedActual = body;

                config.Exists = true;
                // Use non-breaking spaces to allow easier reading in the UI.
                config.Spec = (await this.GetCurrentConfigurationAsync(context, "yaml")).Replace(' ', '\u00a0');
            }

            return config;
        }

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            if (!this.Template.Exists)
            {
                await this.RunKubeCtlAsync(context, "delete", new[]
                {
                    "--ignore-not-found",
                    this.Template.Force ? "--force" : null
                });
            }
            else if (this.Template.Force)
            {
                await this.RunKubeCtlAsync(context, "replace", new[]
                {
                    "--save-config",
                    "--force"
                });
            }
            else
            {
                await this.RunKubeCtlAsync(context, "apply", new string[0]);
            }
        }

        private async Task<string> GetCurrentConfigurationAsync(IOperationExecutionContext context, string outputType)
        {
            using (var sw = new StringWriter())
            {
                await this.RunKubeCtlAsync(context, new[] { "get" }.Concat(this.Template.AddArgs ?? new string[0]).Concat(new[]
                {
                    "--output",
                    outputType,
                    "--ignore-not-found",
                    "--namespace",
                    this.Template.Namespace,
                    "--",
                    this.Template.ResourceType,
                    this.Template.Name
                }), sw.WriteLine);

                return sw.ToString();
            }
        }

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            var config = (KubernetesResourceConfiguration)other;
            if (this.Template.Exists != config.Exists)
                return new ComparisonResult(new[] { new Difference(nameof(config.Exists), this.Template.Exists, config.Exists) });

            if (!this.Template.Exists)
                return ComparisonResult.Identical;

            var actual = JObject.Parse(config.NormalizedActual);
            var template = JObject.Parse(config.NormalizedApplied);

            // Kubernetes has a bad habit of not telling us when immutable properties differ
            // during the dry run. Figure it out ourselves.
            var fresh = JObject.Parse(config.NormalizedTemplate);
            template.Merge(fresh, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Merge });
            // Make sure we don't have any arrays in the template that have extra elements.
            FixArrayMergeLength(template, fresh);

            // We only care about metadata and spec - the other fields are  either part of
            // the configuration key or stats that can change during collection.
            return new ComparisonResult(
                GetJsonDifferences("metadata", template.Property("metadata").Value, actual.Property("metadata").Value)
                .Concat(GetJsonDifferences("spec", template.Property("spec").Value, actual.Property("spec").Value))
            );
        }

        private void FixArrayMergeLength(JToken template, JToken actual)
        {
            if (actual is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    FixArrayMergeLength(template[prop.Name], prop.Value);
                }
            }
            else if (actual is JArray arr)
            {
                while (((JArray)template).Count > arr.Count)
                {
                    ((JArray)template).RemoveAt(arr.Count);
                }

                for (int i = 0; i < arr.Count; i++)
                {
                    FixArrayMergeLength(template[i], arr[i]);
                }
            }
        }

        private static IEnumerable<Difference> GetJsonDifferences(string path, JToken template, JToken actual)
        {
            if (template is JObject objT && actual is JObject objA)
            {
                var keys = objT.Properties().Select(p => p.Name).Union(objA.Properties().Select(p => p.Name));

                foreach (var key in keys)
                {
                    foreach (var diff in GetJsonDifferences(path + "." + key, objT.Property(key)?.Value, objA.Property(key)?.Value))
                    {
                        yield return diff;
                    }
                }
            }
            else if (template is JArray arrT && actual is JArray arrA)
            {
                var len = Math.Max(arrT.Count, arrA.Count);

                for (int i = 0; i < len; i++)
                {
                    foreach (var diff in GetJsonDifferences(path + "[" + i + "]", arrT.ElementAtOrDefault(i), arrA.ElementAtOrDefault(i)))
                    {
                        yield return diff;
                    }
                }
            }
            else if (!JToken.DeepEquals(template, actual))
            {
                yield return new Difference(path, JsonConvert.SerializeObject(template, Formatting.Indented), JsonConvert.SerializeObject(actual, Formatting.Indented));
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure Kubernetes ", new Hilite(config[nameof(this.Template.ResourceType)]),
                    " ", new Hilite(config[nameof(this.Template.Namespace)]), "::", new Hilite(config[nameof(this.Template.Name)])
                )
            );
        }

        private async Task RunKubeCtlAsync(IOperationExecutionContext context, string command, IEnumerable<string> args, Action<string> logOutput = null, Action<string> logError = null)
        {
            var configurationYaml = $"apiVersion: batch/v1\n" +
                $"kind: {JsonConvert.SerializeObject(this.Template.ResourceType)}\n" +
                $"metadata:\n" +
                $"  labels: {JsonConvert.SerializeObject(this.Template.Labels ?? new Dictionary<string, string>())}\n" +
                $"  namespace: {JsonConvert.SerializeObject(this.Template.Namespace)}\n" +
                $"  name: {JsonConvert.SerializeObject(this.Template.Name)}\n" +
                $"spec:\n" +
                $"  " + this.Template.Spec.TrimEnd().Replace("\n", "\n  ") + "\n";

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();

            var baseDir = await fileOps.GetBaseWorkingDirectoryAsync();
            await fileOps.CreateDirectoryAsync(fileOps.CombinePath(baseDir, "scripts"));
            var inputFileName = fileOps.CombinePath(baseDir, "scripts", Guid.NewGuid().ToString("N"));

            try
            {
                await fileOps.WriteAllTextAsync(inputFileName, configurationYaml);

                await this.RunKubeCtlAsync(
                    context,
                    new[] { command }
                    .Concat(this.Template.AddArgs ?? new string[0])
                    .Concat(new[] { "--filename", inputFileName })
                    .Concat(args ?? new string[0]),
                    logOutput ?? this.LogProcessOutput,
                    logError ?? this.LogProcessError
                );
            }
            finally
            {
                await fileOps.DeleteFileAsync(inputFileName);
            }
        }
    }
}
