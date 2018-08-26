using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensions.Kubernetes.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Kubernetes.Configurations
{
    [SlimSerializable]
    [DisplayName("Kubernetes Resource")]
    public sealed class KubernetesResourceConfiguration : PersistedConfiguration, IExistential
    {
        [Required]
        [Persistent]
        [ConfigurationKey]
        [DisplayName("Resource type")]
        [ScriptAlias("Type")]
        [SuggestableValue(typeof(KubernetesResourceTypeSuggestionProvider))]
        public string ResourceType { get; set; }

        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Namespace")]
        [DefaultValue("default")]
        public string Namespace { get; set; } = "default";

        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        public string Name { get; set; }

        [Persistent]
        [ScriptAlias("Labels")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [PlaceholderText("eg. %(component: apiserver, provider: kubernetes)")]
        public IReadOnlyDictionary<string, string> Labels { get; set; }

        [Required]
        [Persistent]
        [DisplayName("Specification")]
        [Category("Configuration")]
        [ScriptAlias("Spec")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [PlaceholderText("eg. $FileContents(pod.json)")]
        public string Spec { get; set; }

        [Persistent]
        [Category("Configuration")]
        [ScriptAlias("Exists")]
        [DefaultValue(true)]
        public bool Exists { get; set; } = true;

        [Persistent]
        [IgnoreConfigurationDrift]
        [Category("Advanced")]
        [DisplayName("Force replacement")]
        [Description("Replace the resource by deleting and re-creating it.")]
        [ScriptAlias("Force")]
        public bool Force { get; set; }

        [Persistent]
        [IgnoreConfigurationDrift]
        [ScriptAlias("AddArgs")]
        [Category("Advanced")]
        [DisplayName("Additional kubectl arguments")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IEnumerable<string> AddArgs { get; set; }

        internal string NormalizedActual { get; set; }
        internal string NormalizedApplied { get; set; }
        internal string NormalizedTemplate { get; set; }
    }
}
