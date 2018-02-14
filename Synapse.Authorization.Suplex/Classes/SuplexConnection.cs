using System;
using System.Collections.Generic;
using System.IO;
using Suplex.Forms.ObjectModel.Api;

using YamlDotNet.Serialization;

public class SuplexConnection
{
    public SuplexConnectionType Type { get; set; } = SuplexConnectionType.File;

    public string Path { get; set; }
    [YamlIgnore]
    public bool HasPath { get { return !string.IsNullOrWhiteSpace( Path ); } }
    [YamlIgnore]
    internal DateTime PathLastWriteTime { get; set; } = DateTime.MinValue;

    SuplexApiClient _splxApi = new SuplexApiClient();
    SuplexStore _splxStore = null;


    public void InitializeChecked()
    {
        switch( Type )
        {
            case SuplexConnectionType.File:
            {
                LoadFileChecked();
                break;
            }
            default:
            {
                //nothing else suported yet, throw exception?
                break;
            }
        }
    }

    bool LoadFileChecked()
    {
        bool ok = false;

        if( HasPath && File.Exists( Path ) )
        {
            DateTime lastWriteTime = File.GetLastWriteTimeUtc( Path );
            if( !lastWriteTime.Equals( PathLastWriteTime ) )
            {
                _splxStore = _splxApi.LoadFile( Path );
                PathLastWriteTime = lastWriteTime;
            }

            ok = true;
        }

        return ok;
    }

    public List<string> GetGroupMembership(string id)
    {
        List<string> list = null;

        if( LoadFileChecked() )
        {
            User user = _splxStore.Users.GetByName( id );
            if( user != null )
            {
                IEnumerable<GroupMembershipItem> membership = _splxStore.GroupMembership.GetByMember( user, false );

                list = new List<string>();
                foreach( GroupMembershipItem g in membership )
                    list.Add( g.Group.Name );
            }
        }

        return list;
    }
}