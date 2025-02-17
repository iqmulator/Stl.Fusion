using System.Reactive;
using System.Runtime.Serialization;
using Stl.CommandR.Commands;
using Stl.Time;

namespace Stl.Fusion.Extensions.Commands
{
    [DataContract]
    public record SetCommand(
        [property: DataMember] string Key,
        [property: DataMember] string Value,
        [property: DataMember] Moment? ExpiresAt = null
        ) : ServerSideCommandBase<Unit>
    {
        public SetCommand() : this("", "") { }
    }
}
