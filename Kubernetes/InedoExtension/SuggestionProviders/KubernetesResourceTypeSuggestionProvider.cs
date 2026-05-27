using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Inedo.Extensibility;
using Inedo.Extensions.Kubernetes.Configurations;
using Inedo.Web;

namespace Inedo.Extensions.Kubernetes.SuggestionProviders;

internal sealed class KubernetesResourceTypeSuggestionProvider : ISuggestionProvider
{
    public IAsyncEnumerable<string> GetSuggestionsAsync(IComponentConfiguration config, CancellationToken cancellationToken)
    {
        var partialType = config[nameof(KubernetesResourceConfiguration.ResourceType)].ToString()?.ToLowerInvariant() ?? string.Empty;

        return standardTypes
            .Where(st => st.full.Contains(partialType, StringComparison.OrdinalIgnoreCase) || (st.abbr?.Contains(partialType) ?? false))
            .Select(st => st.full)
            .ToAsyncEnumerable();
    }

    private static readonly (string full, string abbr)[] standardTypes =
    [
        ("CertificateSigningRequest", "csr"),
        ("ClusterRoleBinding", null),
        ("ClusterRole", null),
        ("ComponentStatus", "cs"),
        ("ConfigMap", "cm"),
        ("ControllerRevision", null),
        ("CronJob", null),
        ("CustomResourceDefinition", "crd"),
        ("DaemonSet", "ds"),
        ("Deployment", "deploy"),
        ("Endpoint", "ep"),
        ("Event", "ev"),
        ("HorizontalPodAutoscaler", "hpa"),
        ("Ingress", "ing"),
        ("Job", null),
        ("LimitRange", "limits"),
        ("Namespace", "ns"),
        ("NetworkPolicy", "netpol"),
        ("Node", "no"),
        ("PersistentVolumeClaim", "pvc"),
        ("PersistentVolume", "pv"),
        ("PodDisruptionBudget", "pdb"),
        ("PodPreset", null),
        ("Pod", "po"),
        ("PodSecurityPolicy", "psp"),
        ("PodTemplate", null),
        ("ReplicaSet", "rs"),
        ("ReplicationController", "rc"),
        ("ResourceQuota", "quota"),
        ("RoleBinding", null),
        ("Role", null),
        ("Secret", null),
        ("ServiceAccount", "sa"),
        ("Service", "svc"),
        ("StatefulSet", "sts"),
        ("StorageClass", "sc")
    ];
}
