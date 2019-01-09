using System;
using System.Collections.Generic;
using System.IO;

using Suplex.Security.DataAccess;
using Suplex.Security.Principal;
using Suplex.Security.WebApi;

using YamlDotNet.Serialization;

public class SuplexConnection
{
    public SuplexDalConnectionType Type { get; set; } = SuplexDalConnectionType.File;

    public string Path { get; set; }
    [YamlIgnore]
    public bool HasPath { get { return !string.IsNullOrWhiteSpace( Path ); } }
    [YamlIgnore]
    internal DateTime PathLastWriteTime { get; set; } = DateTime.MinValue;

    ISuplexDal _dal = null;


    public bool WantsInitialize
    {
        get
        {
            switch( Type )
            {
                case SuplexDalConnectionType.File:
                {
                    if( HasPath && File.Exists( Path ) )
                    {
                        DateTime lastWriteTime = File.GetLastWriteTimeUtc( Path );
                        return !lastWriteTime.Equals( PathLastWriteTime );
                    }
                    return false;
                }
                default:
                {
                    return false;
                }
            }
        }
    }

    public void Initialize()
    {
        switch( Type )
        {
            case SuplexDalConnectionType.File:
            {
                _dal = FileSystemDal.LoadFromYamlFile( Path );
                PathLastWriteTime = File.GetLastWriteTimeUtc( Path );
                break;
            }
            case SuplexDalConnectionType.RestApi:
            {
                _dal = new SuplexSecurityHttpApiClient( Path );
                break;
            }
        }
    }

    public List<string> GetGroupMembership(string id)
    {
        List<string> list = null;

        List<User> users = _dal.GetUserByName( id, exact: true );
        if( users?.Count > 0 )
        {
            IEnumerable<GroupMembershipItem> membership = _dal.GetGroupMemberOf( users[0].UId, false );

            list = new List<string>();
            foreach( GroupMembershipItem g in membership )
                list.Add( g.Group.Name );
        }

        return list;
    }
}