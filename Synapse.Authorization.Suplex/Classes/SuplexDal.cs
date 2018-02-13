﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using io = System.IO;

using Suplex.Forms.ObjectModel.Api;

namespace Synapse.Authorization.Suplex
{
    public partial class SuplexDal
    {
        SuplexApiClient _splxApi = new SuplexApiClient();
        SuplexStore _splxStore = null;

        SuplexConnectionInfo _connectionInfo = null;


        public SuplexDal(SuplexConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
        }

        bool LoadFileChecked()
        {
            bool ok = false;

            if( _connectionInfo.HasPath && File.Exists( _connectionInfo.Path ) )
            {
                DateTime lastWriteTime = File.GetLastWriteTimeUtc( _connectionInfo.Path );
                if( !lastWriteTime.Equals( _connectionInfo.PathLastWriteTime ) )
                {
                    _splxStore = _splxApi.LoadFile( _connectionInfo.Path );
                    _connectionInfo.PathLastWriteTime = lastWriteTime;
                }

                ok = true;
            }

            return ok;
        }

        public bool IsFileStore { get; private set; }

        public List<string> GetGroupMembership(string id)
        {
            if( LoadFileChecked() )
            {
            }

            return null;
        }
    }
}