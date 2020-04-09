using System;
using System.Collections.Generic;
using Inedo.Extensibility.PackageContainerScanners;

namespace Inedo.Extensions.Kubernetes.Scanners
{
    public class KubernetesContainerUsageData : ContainerUsageData
    {
        public KubernetesContainerUsageData(string server, string name, string imageId, ContainerState? state, DateTimeOffset? created)
        {
            Server = server;
            Name = name;
            ImageId = imageId;
            State = state;
            Created = created;
        }

        public override string Server { get; }

        public override string Name { get; }

        public override string ImageId { get; }

        public override ContainerState? State { get; }

        public override DateTimeOffset? Created { get; }

        public override bool Equals(object obj)
        {
            return obj is KubernetesContainerUsageData data &&
                   Server == data.Server &&
                   Name == data.Name &&
                   ImageId == data.ImageId;
        }

        public override int GetHashCode()
        {
            int hashCode = -1425002177;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Server);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ImageId);
            return hashCode;
        }
    }
}
