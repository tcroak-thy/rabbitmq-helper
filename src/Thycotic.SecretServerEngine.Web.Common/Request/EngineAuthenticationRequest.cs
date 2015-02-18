﻿using System;

namespace Thycotic.SecretServerEngine.Web.Common.Request
{
    /// <summary>
    /// Engine authentication request when an engine tries to manipulate contents of an exchange.
    /// </summary>
    public class EngineAuthenticationRequest : EngineRequestBase
    {
        /// <summary>
        /// Gets or sets the name of the exchange.
        /// </summary>
        /// <value>
        /// The name of the exchange.
        /// </value>
        public string ExchangeName { get; set; }
    }
}