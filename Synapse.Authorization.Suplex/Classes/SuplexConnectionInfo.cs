using System;

using YamlDotNet.Serialization;

public class SuplexConnectionInfo
{
    public SuplexConnectionType Type { get; set; } = SuplexConnectionType.File;

    public string Path { get; set; }
    [YamlIgnore]
    public bool HasPath { get { return !string.IsNullOrWhiteSpace( Path ); } }
    [YamlIgnore]
    internal DateTime PathLastWriteTime { get; set; } = DateTime.MinValue;
}