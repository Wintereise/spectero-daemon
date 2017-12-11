﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ServiceStack.DataAnnotations;

namespace Spectero.daemon.Models
{
    public class User : IModel
    {
        [EnumAsInt]
        public enum SourceTypes
        {
            Local,
            SpecteroCloud
        }

        [EnumAsInt]
        public enum Role
        {
            SuperAdmin,
            WebApi,
            HTTPProxy,
            OpenVPN,
            ShadowSOCKS,
            SSHTunnel
        }

 

        [Index]
        [AutoIncrement]
        public long Id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SourceTypes Source { get; set; }

        [ServiceStack.DataAnnotations.Required]
        [ServiceStack.DataAnnotations.StringLength(50)]
        [Index(Unique = true)]
        public string AuthKey { get; set; }

        [JsonProperty("roles", ItemConverterType = typeof(StringEnumConverter))]
        public List<Role> Roles { get; set; }

        [JsonProperty("password")]
        [Ignore]
        public string PasswordSetter {  set => Password = value; }

        [ServiceStack.DataAnnotations.Required]
        [JsonIgnore] // Prevent JSON serialization
        public string Password { get; set; }

        public string Cert { get; set; }

        [JsonIgnore] // Prevent JSON serialization
        public string CertKey { get; set; }

        public long SpecteroEngagementId = 0;

        [DataType(DataType.Date)]
        public DateTime CreatedDate{ get; set; }

        [DataType(DataType.Date)]
        public DateTime LastLoginTime { get; set; }

        public override string ToString()
        {
            return "Id -> " + Id + ", AuthKey -> " + AuthKey + ", Password -> " + Password + ", Created At -> " + CreatedDate;
        }

        public enum Actions
        {
            ManageDaemon,
            ManageApi,
            ConnectToHTTPProxy,
            ConnectToOpenVPN,
            ConnectToShadowSOCKS,
            ConnectToSSHTunnel
        }

        private bool HasRole(Role role)
        {
            if (Roles == null || Roles.Count == 0)
                return false;

            return Roles.Contains(role);
        }

        /*
         * Poor man's RBAC, our needs are not big enough to use a proper roles framework.
         */
        public bool Can(Actions action)
        {
            if (HasRole(Role.SuperAdmin))
                return true;

            if (HasRole(Role.WebApi))
            {
                if (action != Actions.ManageDaemon)
                    return true;
                return false;
            }

            if (HasRole(Role.HTTPProxy) && action.Equals(Actions.ConnectToHTTPProxy))
                return true;

            if (HasRole(Role.OpenVPN) && action.Equals(Actions.ConnectToOpenVPN))
                return true;

            if (HasRole(Role.ShadowSOCKS) && action.Equals(Actions.ConnectToShadowSOCKS))
                return true;

            if (HasRole(Role.SSHTunnel) && action.Equals(Actions.ConnectToSSHTunnel))
                return true;

            return false;
        }
    }
}