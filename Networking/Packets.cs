using ProtoBuf;

namespace AlmanacCodex.Networking;

public static class NetworkChannels
{
    public const string Discovery = "almanaccodex.discovery";
}

[ProtoContract]
public class SightPacket
{
    [ProtoMember(1)]
    public string Code { get; set; } = "";
}

[ProtoContract]
public class HeldPacket
{
    [ProtoMember(1)]
    public string Code { get; set; } = "";
}

[ProtoContract]
public class ProcessPacket
{
    [ProtoMember(1)]
    public string Code { get; set; } = "";

    [ProtoMember(2)]
    public string ProcessCode { get; set; } = "";
}
