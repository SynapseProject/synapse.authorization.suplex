using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Synapse.Authorization;
using Synapse.Core.Utilities;
using Synapse.Services;

using YamlDotNet.Serialization;

public class SuplexProvider : IAuthorizationProvider
{
    static Dictionary<int, SuplexProvider> _cache = new Dictionary<int, SuplexProvider>();

    public SuplexConnection Connection { get; set; }
    [YamlIgnore]
    public bool HasConnection { get { return Connection != null && Connection.HasPath; } }

    public PrincipalList Users { get; set; }
    [YamlIgnore]
    public bool HasUsers { get { return Users != null && Users.HasContent; } }
    [YamlIgnore]
    public bool HasUsersAllowed { get { return Users != null && Users.HasAllowed; } }
    [YamlIgnore]
    public bool HasUsersDenied { get { return Users != null && Users.HasDenied; } }

    public PrincipalList Groups { get; set; }
    [YamlIgnore]
    public bool HasGroups { get { return Groups != null && Groups.HasContent; } }
    [YamlIgnore]
    public bool HasGroupsAllowed { get { return Groups != null && Groups.HasAllowed; } }
    [YamlIgnore]
    public bool HasGroupsDenied { get { return Groups != null && Groups.HasDenied; } }

    public string ListSourcePath { get; set; }
    [YamlIgnore]
    internal DateTime ListSourceLastWriteTime { get; set; } = DateTime.MinValue;

    private bool HasContent { get { return HasUsers || HasGroups; } }


    public void Configure(IAuthorizationProviderConfig conifg)
    {
        if( conifg != null )
        {
            int hash = conifg.Config.GetHashCode();
            if( !_cache.ContainsKey( hash ) || _cache[hash] == null )
            {
                string s = YamlHelpers.Serialize( conifg.Config );
                _cache[hash] = YamlHelpers.Deserialize<SuplexProvider>( s );
            }

            Configure( _cache[hash] );

            //if external source declared, merge contents
            if( !string.IsNullOrWhiteSpace( ListSourcePath ) && File.Exists( ListSourcePath ) )
            {
                DateTime lastWriteTime = File.GetLastWriteTimeUtc( ListSourcePath );
                if( !lastWriteTime.Equals( ListSourceLastWriteTime ) )
                {
                    string s = YamlHelpers.Serialize( conifg.Config );
                    SuplexProvider p = YamlHelpers.Deserialize<SuplexProvider>( s );
                    Configure( p, lastWriteTime );


                    SuplexProvider listSource = YamlHelpers.DeserializeFile<SuplexProvider>( ListSourcePath );

                    if( listSource.HasUsers )
                    {
                        EnsureUsersGroups( isUsers: true );

                        if( listSource.Users.HasAllowed )
                            Users.Allowed.AddRange( listSource.Users.Allowed );

                        if( listSource.Users.HasDenied )
                            Users.Denied.AddRange( listSource.Users.Denied );
                    }
                    if( listSource.HasGroups )
                    {
                        EnsureUsersGroups( isUsers: false );

                        if( listSource.Groups.HasAllowed )
                            Groups.Allowed.AddRange( listSource.Groups.Allowed );

                        if( listSource.Groups.HasDenied )
                            Groups.Denied.AddRange( listSource.Groups.Denied );
                    }

                    _cache[hash] = this;
                }
            }

            if( HasConnection )
            {
                Connection.InitializeChecked();
                _cache[hash] = this;
            }
        }
    }

    private void Configure(SuplexProvider p, DateTime? listSourceLastWriteTime = null)
    {
        Connection = p.Connection;
        Users = p.Users;
        Groups = p.Groups;
        ListSourcePath = p.ListSourcePath;
        ListSourceLastWriteTime = listSourceLastWriteTime ?? p.ListSourceLastWriteTime;
    }

    private void EnsureUsersGroups(bool isUsers)
    {
        if( isUsers )
        {
            if( Users == null )
                Users = new PrincipalList();
            if( Users.Allowed == null )
                Users.Allowed = new List<string>();
            if( Users.Denied == null )
                Users.Denied = new List<string>();
        }

        if( !isUsers )
        {
            if( Groups == null )
                Groups = new PrincipalList();
            if( Groups.Allowed == null )
                Groups.Allowed = new List<string>();
            if( Groups.Denied == null )
                Groups.Denied = new List<string>();
        }
    }


    public object GetDefaultConfig()
    {
        return new SuplexProvider();
    }

    public AuthorizationType IsAuthorized(string id)
    {
        if( !HasContent )
            return AuthorizationType.ImplicitAllow;

        bool? found = null;
        AuthorizationType result = AuthorizationType.None;

        List<string> groupMembership = null;
        if( HasGroups && HasConnection )
            groupMembership = Connection.GetGroupMembership( id );
        bool haveGroupMembership = groupMembership != null && groupMembership.Count > 0;

        //process Denies
        if( HasUsersDenied )
        {
            found = Users.Denied.Contains( id, StringComparer.OrdinalIgnoreCase );
            if( found.HasValue && found.Value )
                return AuthorizationType.ExplicitDeny; //explicit deny is final, return now
        }

        if( HasGroupsDenied )
        {
            if( haveGroupMembership )
            {
                IEnumerable<string> denied = from member in groupMembership
                                             join grp in Groups.Denied
                                             on member.ToLower() equals grp.ToLower()
                                             select member;
                found = denied.Count() > 0;
                if( found.Value )
                    return AuthorizationType.ExplicitDeny;  //explicit deny is final, return now
            }
            else
                result |= AuthorizationType.ImplicitAllow;  //no groupMembership == implied Allow (user not specifically denied)
        }

        //process Allows
        if( HasUsersAllowed )
        {
            found = Users.Allowed.Contains( id, StringComparer.OrdinalIgnoreCase );
            if( found.HasValue && found.Value )
                result |= AuthorizationType.ExplicitAllow;
        }

        if( HasGroupsAllowed )
        {
            if( haveGroupMembership )
            {
                IEnumerable<string> allowed = from member in groupMembership
                                              join grp in Groups.Allowed
                                              on member.ToLower() equals grp.ToLower()
                                              select member;
                found = allowed.Count() > 0;
                if( found.Value )
                    result |= AuthorizationType.ExplicitAllow;
            }
            else
                result |= AuthorizationType.ImplicitDeny;  //no groupMembership == implied Deny
        }

        //if we got here, the user id wasn't specified in Denied and wasn't specifically Allowed
        //if either of these is true (HasUsers/HasGroups), we take omission as implied Deny
        if( (HasUsersAllowed || HasGroupsAllowed) && (result & AuthorizationType.GeneralAllow) != AuthorizationType.GeneralAllow )
            result |= AuthorizationType.ImplicitDeny;

        return result;
    }
}