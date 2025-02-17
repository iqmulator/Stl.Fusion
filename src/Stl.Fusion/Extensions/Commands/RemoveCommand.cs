using System.Reactive;
using System.Runtime.Serialization;
using Stl.CommandR.Commands;

namespace Stl.Fusion.Extensions.Commands
{
    [DataContract]
    public record RemoveCommand(
        [property: DataMember] string Key
        ) : ServerSideCommandBase<Unit>
    {
        public RemoveCommand() : this("") { }
    }
}
