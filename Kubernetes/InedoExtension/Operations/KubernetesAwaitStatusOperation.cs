using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Kubernetes.SuggestionProviders;
using Inedo.Web;
using Newtonsoft.Json;

namespace Inedo.Extensions.Kubernetes.Operations
{
    [DisplayName("Await Kubernetes Resource Status")]
    [Description("Waits for a Kubernetes resource to arrive at a specific status.")]
    [ScriptAlias("Await-Status")]
    [Tag("kubernetes")]
    public sealed class KubernetesAwaitStatusOperation : ExecuteOperation
    {
        [Required]
        [DisplayName("Resource type")]
        [ScriptAlias("Type")]
        [SuggestableValue(typeof(KubernetesResourceTypeSuggestionProvider))]
        public string ResourceType { get; set; }

        [ScriptAlias("Namespace")]
        [DefaultValue("default")]
        public string Namespace { get; set; } = "default";

        [Required]
        [ScriptAlias("Name")]
        public string Name { get; set; }

        [Required]
        [DisplayName("Condition")]
        [ScriptAlias("Condition")]
        [PlaceholderText("eg. Complete")]
        public string ConditionType { get; set; }

        [Required]
        [DisplayName("Expected status")]
        [ScriptAlias("Status")]
        [PlaceholderText("eg. True")]
        public string ExpectedStatus { get; set; }

        [Category("Advanced")]
        [ScriptAlias("AddArgs")]
        [DisplayName("Additional kubectl arguments")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IEnumerable<string> AddArgs { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            using (var cts = new CancellationTokenSource())
            using (context.CancellationToken.Register(() => cts.Cancel()))
            {
                bool success = false;
                try
                {
                    await this.RunKubeCtlAsync(context,
                        new[] { "get" }.Concat(this.AddArgs ?? new string[0]).Concat(new[]
                        {
                            "--watch",
                            "--output",
                            "jsonpath={.status.conditions[?(@.type==" + JsonConvert.SerializeObject(this.ConditionType) + ")].status}\n",
                            "--namespace",
                            this.Namespace,
                            this.ResourceType,
                            this.Name
                        }),
                        logOutput: status =>
                        {
                            if (this.CheckStatus(status))
                            {
                                success = true;
                                cts.Cancel();
                            }
                        },
                        handleExit: code => success,
                        cancellationToken: cts.Token
                    );
                }
                catch (OperationCanceledException) when (success)
                {
                }
            }
        }

        private bool CheckStatus(string status)
        {
            this.LogDebug($"Current \"{this.ConditionType}\" status is \"{status}\"");

            return string.Equals(this.ExpectedStatus, status, StringComparison.OrdinalIgnoreCase);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Wait for ", new Hilite(config[nameof(this.ConditionType)]),
                    " to be \"", new Hilite(config[nameof(this.ExpectedStatus)]),
                    "\" on Kubernetes ", new Hilite(config[nameof(this.ResourceType)]),
                    " ", new Hilite(config[nameof(this.Namespace)]), "::", new Hilite(config[nameof(this.Name)])
                )
            );
        }
    }
}
