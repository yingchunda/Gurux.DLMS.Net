//
// --------------------------------------------------------------------------
//  Gurux Ltd
//
//
//
// Filename:        $HeadURL$
//
// Version:         $Revision$,
//                  $Date$
//                  $Author$
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// More information of Gurux products: https://www.gurux.org
//
// This code is licensed under the GNU General Public License v2.
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using Gurux.DLMS.Internal;
using Gurux.DLMS.Objects;
using Gurux.DLMS.ManufacturerSettings;
using Gurux.DLMS.Secure;
using Gurux.DLMS.Enums;

namespace Gurux.DLMS
{
    public delegate void PduEventHandler(object sender, byte[] data);

    /// <summary>
    /// GXDLMS implements methods to communicate with DLMS/COSEM metering devices.
    /// </summary>
    public class GXDLMSClient
    {
        protected GXDLMSTranslator translator;
        internal PduEventHandler pdu;

        public event PduEventHandler OnPdu
        {
            add
            {
                pdu += value;
            }
            remove
            {
                pdu -= value;
            }
        }

        /// <summary>
        /// XML client don't throw exceptions. It serializes them as a default. Set value to true, if exceptions are thrown.
        /// </summary>
        protected bool throwExceptions;

        /// <summary>
        /// DLMS settings.
        /// </summary>
        public GXDLMSSettings Settings
        {
            get;
            private set;
        }

        private static Dictionary<ObjectType, Type> AvailableObjectTypes = new Dictionary<ObjectType, Type>();
        /// <summary>
        /// Standard OBIS code
        /// </summary>
        private static GXStandardObisCodeCollection codes = new GXStandardObisCodeCollection();

        /// <summary>
        /// Static Constructor. This is called only once. Get available COSEM objects.
        /// </summary>
        static GXDLMSClient()
        {
            GXDLMS.GetAvailableObjects(AvailableObjectTypes);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GXDLMSClient() : this(false)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="useLogicalNameReferencing">Is Logical or short name referencing used.</param>
        /// <param name="clientAddress">Client address. Default is 16 (0x10)</param>
        /// <param name="serverAddress">Server ID. Default is 1.</param>
        /// <param name="authentication">Authentication type. Default is None</param>
        /// <param name="password">Password if authentication is used.</param>
        /// <param name="interfaceType">Interface type. Default is general.</param>
        public GXDLMSClient(bool useLogicalNameReferencing) : this(useLogicalNameReferencing, 16, 1, Authentication.None, null, InterfaceType.HDLC)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="useLogicalNameReferencing">Is Logical or short name referencing used.</param>
        /// <param name="clientAddress">Client address. Default is 16 (0x10)</param>
        /// <param name="serverAddress">Server ID. Default is 1.</param>
        /// <param name="authentication">Authentication type. Default is None</param>
        /// <param name="password">Password if authentication is used.</param>
        /// <param name="interfaceType">Interface type. Default is general.</param>
        public GXDLMSClient(bool useLogicalNameReferencing,
                            int clientAddress, int serverAddress, Authentication authentication,
                            string password, InterfaceType interfaceType)
        {
            Settings = new GXDLMSSettings(false);
            Settings.Objects.Parent = this;
            Settings.UseLogicalNameReferencing = useLogicalNameReferencing;
            Settings.InterfaceType = interfaceType;
            Settings.Authentication = authentication;
            Settings.ServerAddress = serverAddress;
            Settings.ClientAddress = clientAddress;
            if (password != null)
            {
                Settings.Password = ASCIIEncoding.ASCII.GetBytes(password);
            }
        }

        /// <summary>
        /// Copy client settings.
        /// </summary>
        /// <param name="target"></param>
        public void CopyTo(GXDLMSClient target)
        {
            Settings.CopyTo(target.Settings);
        }


        /// <summary>
        /// List of available custom obis codes.
        /// </summary>
        /// <remarks>
        /// This list is used when Association view is read from the meter and description of the object is needed.
        /// If collection is not set description of object is empty.
        /// </remarks>
        public GXObisCodeCollection CustomObisCodes
        {
            get;
            set;
        }

        /// <summary>
        /// Client address.
        /// </summary>
        public int ClientAddress
        {
            get
            {
                return Settings.ClientAddress;
            }
            set
            {
                Settings.ClientAddress = value;
            }
        }

        /// <summary>
        /// GBT window size.
        /// </summary>
        public byte WindowSize
        {
            get
            {
                return Settings.WindowSize;
            }
            set
            {
                Settings.WindowSize = value;
            }
        }

        /// <summary>
        /// Standard says that Time zone is from normal time to UTC in minutes.
        /// If meter is configured to use UTC time (UTC to normal time) set this to true.
        /// Example. Italy, Saudi Arabia and India standards are using UTC time zone, not DLMS standard time zone.
        /// </summary>
        public bool UtcTimeZone
        {
            get
            {
                return Settings.UseUtc2NormalTime;
            }
            set
            {
                Settings.UseUtc2NormalTime = value;
            }
        }

        /// <summary>
        /// Used standard.
        /// </summary>
        public Standard Standard
        {
            get
            {
                return Settings.Standard;
            }
            set
            {
                Settings.Standard = value;
            }
        }

        /// <summary>
        /// Force that data is always sent as blocks.
        /// </summary>
        /// <remarks>
        /// Some meters can handle only blocks. This property is used to force send all data in blocks.
        /// </remarks>
        public bool ForceToBlocks
        {
            get
            {
                return Settings.ForceToBlocks;
            }
            set
            {
                Settings.ForceToBlocks = value;
            }
        }

        /// <summary>
        /// Quality of service.
        /// </summary>
        [DefaultValue(0)]
        public byte QualityOfService
        {
            get
            {
                return Settings.QualityOfService;
            }
            set
            {
                Settings.QualityOfService = value;
            }
        }

        /// <summary>
        /// User id is the identifier of the user.
        /// </summary>
        /// <remarks>
        /// This value is used if user list on Association LN is used.
        /// </remarks>
        [DefaultValue(-1)]
        public int UserId
        {
            get
            {
                return Settings.UserId;
            }
            set
            {
                if (value < -1 || value > 255)
                {
                    throw new ArgumentOutOfRangeException("Invalid user Id.");
                }
                Settings.UserId = value;
            }
        }

        /// <summary>
        /// Server address.
        /// </summary>
        public int ServerAddress
        {
            get
            {
                return Settings.ServerAddress;
            }
            set
            {
                Settings.ServerAddress = value;
            }
        }

        /// <summary>
        /// Size of server address.
        /// </summary>
        public byte ServerAddressSize
        {
            get
            {
                return Settings.ServerAddressSize;
            }
            set
            {
                Settings.ServerAddressSize = value;
            }
        }

        /// <summary>
        ///  Source system title.
        /// </summary>
        /// <remarks>
        /// Meter returns system title when ciphered connection is made or GMAC authentication is used.
        /// </remarks>
        public byte[] SourceSystemTitle
        {
            get
            {
                return Settings.SourceSystemTitle;
            }
        }

        /// <summary>
        /// DLMS version number.
        /// </summary>
        /// <remarks>
        /// Gurux DLMS component supports DLMS version number 6.
        /// </remarks>
        /// <seealso cref="SNRMRequest"/>
        [DefaultValue(6)]
        public byte DLMSVersion
        {
            get
            {
                return Settings.DLMSVersion;
            }
        }

        /// <summary>
        /// Retrieves the maximum size of PDU receiver.
        /// </summary>
        /// <remarks>
        /// PDU size tells maximum size of PDU packet.
        /// Value can be from 0 to 0xFFFF. By default the value is 0xFFFF.
        /// </remarks>
        /// <seealso cref="ClientAddress"/>
        /// <seealso cref="ServerAddress"/>
        /// <seealso cref="DLMSVersion"/>
        /// <seealso cref="UseLogicalNameReferencing"/>
        [DefaultValue(0xFFFF)]
        public ushort MaxReceivePDUSize
        {
            get
            {
                return Settings.MaxPduSize;
            }
            set
            {
                Settings.MaxPduSize = value;
            }
        }

        /// <summary>
        /// Determines, whether Logical, or Short name, referencing is used.
        /// </summary>
        /// <remarks>
        /// Referencing depends on the device to communicate with.
        /// Normally, a device supports only either Logical or Short name referencing.
        /// The referencing is defined by the device manufacurer.
        /// If the referencing is wrong, the SNMR message will fail.
        /// </remarks>
        [DefaultValue(false)]
        public bool UseLogicalNameReferencing
        {
            get
            {
                return Settings.UseLogicalNameReferencing;
            }
            set
            {
                Settings.UseLogicalNameReferencing = value;
            }
        }

        /// <summary>
        /// Client to Server custom challenge.
        /// </summary>
        /// <remarks>
        /// This is for debugging purposes. Reset custom challenge settings CtoSChallenge to null.
        /// </remarks>
        public byte[] CtoSChallenge
        {
            get
            {
                return Settings.CtoSChallenge;
            }
            set
            {
                Settings.UseCustomChallenge = value != null;
                Settings.CtoSChallenge = value;
            }
        }

        /// <summary>
        /// Retrieves the password that is used in communication.
        /// </summary>
        /// <remarks>
        /// If authentication is set to none, password is not used.
        /// For HighSHA1, HighMD5 and HighGMAC password is worked as a shared secret.
        /// </remarks>
        /// <seealso cref="Authentication"/>
        public byte[] Password
        {
            get
            {
                return Settings.Password;
            }
            set
            {
                Settings.Password = value;
            }
        }

        /// <summary>
        /// When connection is made client tells what kind of services it want's to use.
        /// </summary>
        public Conformance ProposedConformance
        {
            get
            {
                return Settings.ProposedConformance;
            }
            set
            {
                Settings.ProposedConformance = value;
            }
        }

        /// <summary>
        /// Functionality what server can offer.
        /// </summary>
        public Conformance NegotiatedConformance
        {
            get
            {
                return Settings.NegotiatedConformance;
            }
            set
            {
                Settings.NegotiatedConformance = value;
            }
        }

        /// <summary>
        /// Protocol version.
        /// </summary>
        public string ProtocolVersion
        {
            get
            {
                return Settings.protocolVersion;
            }
            set
            {
                Settings.protocolVersion = value;
            }
        }

        /// <summary>
        /// Retrieves the authentication used in communicating with the device.
        /// </summary>
        /// <remarks>
        /// By default authentication is not used. If authentication is used,
        /// set the password with the Password property.
        /// Note!
        /// For HLS authentication password (shared secret) is needed from the manufacturer.
        /// </remarks>
        /// <seealso cref="Password"/>
        /// <seealso cref="ClientAddress"/>
        /// <seealso cref="ServerAddress"/>
        /// <seealso cref="DLMSVersion"/>
        /// <seealso cref="UseLogicalNameReferencing"/>
        /// <seealso cref="MaxReceivePDUSize"/>
        [DefaultValue(Authentication.None)]
        public Authentication Authentication
        {
            get
            {
                return Settings.Authentication;
            }
            set
            {
                Settings.Authentication = value;
            }
        }

        /// <summary>
        /// Set starting block index in HDLC framing.
        /// Default is One based, but some meters use Zero based value.
        /// Usually this is not used.
        /// </summary>
        public UInt32 StartingBlockIndex
        {
            get
            {
                return Settings.StartingBlockIndex;
            }
            set
            {
                Settings.StartingBlockIndex = value;
                Settings.ResetBlockIndex();
            }
        }

        /// <summary>
        /// Used priority in HDLC framing.
        /// </summary>
        public Priority Priority
        {
            get
            {
                return Settings.Priority;
            }
            set
            {
                Settings.Priority = value;
            }
        }

        /// <summary>
        /// Used service class in HDLC framing.
        /// </summary>
        public ServiceClass ServiceClass
        {
            get
            {
                return Settings.ServiceClass;
            }
            set
            {
                Settings.ServiceClass = value;
            }
        }

        /// <summary>
        /// Invoke ID.
        /// </summary>
        public byte InvokeID
        {
            get
            {
                return Settings.InvokeID;
            }
            set
            {
                Settings.InvokeID = value;
            }
        }

        /// <summary>
        /// Auto increase Invoke ID.
        /// </summary>
        [DefaultValue(false)]
        public bool AutoIncreaseInvokeID
        {
            get;
            set;
        }

        /// <summary>
        /// Determines the type of the connection
        /// </summary>
        /// <remarks>
        /// All DLMS meters do not support the IEC 62056-47 standard.
        /// If the device does not support the standard, and the connection is made
        /// using TCP/IP, set the type to InterfaceType.General.
        /// </remarks>
        public InterfaceType InterfaceType
        {
            get
            {
                return Settings.InterfaceType;
            }
            set
            {
                Settings.InterfaceType = value;
            }
        }

        /// <summary>
        /// Information from the connection size that server can handle.
        /// </summary>
        public GXDLMSLimits Limits
        {
            get
            {
                return Settings.Limits;
            }
        }

        /// <summary>
        /// Gateway settings.
        /// </summary>
        public GXDLMSGateway Gateway
        {
            get
            {
                return Settings.Gateway;
            }
            set
            {
                Settings.Gateway = value;
            }
        }

        /// <summary>
        /// Connection state to the meter.
        /// </summary>
        public ConnectionState ConnectionState
        {
            get
            {
                return Settings.Connected;
            }
        }

        /// <summary>
        /// Generates SNRM request.
        /// </summary>
        /// <remarks>
        /// his method is used to generate send SNRMRequest.
        /// Before the SNRM request can be generated, at least the following
        /// properties must be set:
        /// <ul>
        /// <li>ClientAddress</li>
        /// <li>ServerAddress</li>
        /// </ul>
        /// <b>Note! </b>According to IEC 62056-47: when communicating using
        /// TCP/IP, the SNRM request is not send.
        /// </remarks>
        /// <returns>SNRM request as byte array.</returns>
        /// <seealso cref="ClientAddress"/>
        /// <seealso cref="ServerAddress"/>
        /// <seealso cref="ParseUAResponse"/>
        public byte[] SNRMRequest()
        {
            Settings.Connected = ConnectionState.None;
            IsAuthenticationRequired = false;
            // SNRM request is not used in network connections.
            if (InterfaceType != InterfaceType.HDLC)
            {
                return null;
            }
            GXByteBuffer data = new GXByteBuffer(25);
            data.SetUInt8(0x81); // FromatID
            data.SetUInt8(0x80); // GroupID
            data.SetUInt8(0); // Length.
            int maxInfoTX = Limits.MaxInfoTX, maxInfoRX = Limits.MaxInfoRX;
            if (Limits.UseFrameSize)
            {
                byte[] primaryAddress, secondaryAddress;
                primaryAddress = GXDLMS.GetHdlcAddressBytes(Settings.ServerAddress, Settings.ServerAddressSize);
                secondaryAddress = GXDLMS.GetHdlcAddressBytes(Settings.ClientAddress, 0);
                maxInfoTX -= (10 + secondaryAddress.Length);
                maxInfoRX -= (10 + primaryAddress.Length);
            }

            // If custom HDLC parameters are used.
            if (GXDLMSLimitsDefault.DefaultMaxInfoTX != maxInfoTX ||
                GXDLMSLimitsDefault.DefaultMaxInfoRX != maxInfoRX ||
                GXDLMSLimitsDefault.DefaultWindowSizeTX != Limits.WindowSizeTX ||
                GXDLMSLimitsDefault.DefaultWindowSizeRX != Limits.WindowSizeRX)
            {
                data.SetUInt8((byte)HDLCInfo.MaxInfoTX);
                GXDLMS.AppendHdlcParameter(data, (UInt16)maxInfoTX);
                data.SetUInt8((byte)HDLCInfo.MaxInfoRX);
                GXDLMS.AppendHdlcParameter(data, (UInt16)maxInfoRX);
                data.SetUInt8((byte)HDLCInfo.WindowSizeTX);
                data.SetUInt8(4);
                data.SetUInt32(Limits.WindowSizeTX);
                data.SetUInt8((byte)HDLCInfo.WindowSizeRX);
                data.SetUInt8(4);
                data.SetUInt32(Limits.WindowSizeRX);
            }
            // If default HDLC parameters are not used.
            if (data.Size != 3)
            {
                data.SetUInt8(2, (byte)(data.Size - 3)); // Length.
            }
            else
            {
                data = null;
            }
            Settings.ResetFrameSequence();
            return GXDLMS.GetHdlcFrame(Settings, (byte)Command.Snrm, data);
        }

        /// <summary>
        /// Parses UAResponse from byte array.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="data"></param>
        /// <seealso cref="ParseUAResponse"/>
        public void ParseUAResponse(GXByteBuffer data)
        {
            GXDLMS.ParseSnrmUaResponse(data, Settings);
            Settings.Connected = ConnectionState.Hdlc;
        }

        /// <summary>
        /// Generate AARQ request.
        /// </summary>
        /// <returns>AARQ request as byte array.</returns>
        /// <seealso cref="ParseAAREResponse"/>
        public byte[][] AARQRequest()
        {
            Settings.NegotiatedConformance = (Conformance)0;
            Settings.ResetBlockIndex();
            Settings.Connected &= ~ConnectionState.Dlms;
            GXByteBuffer buff = new GXByteBuffer(20);
            GXDLMS.CheckInit(Settings);
            Settings.StoCChallenge = null;
            if (AutoIncreaseInvokeID)
            {
                Settings.InvokeID = 0;
            }
            else
            {
                Settings.InvokeID = 1;
            }
            //If High authentication is used.
            if (Authentication > Authentication.Low)
            {
                if (!Settings.UseCustomChallenge)
                {
                    Settings.CtoSChallenge = GXSecure.GenerateChallenge(Settings.Authentication);
                }
            }
            else
            {
                Settings.CtoSChallenge = null;
            }
            GXAPDU.GenerateAarq(Settings, Settings.Cipher, null, buff);
            byte[][] reply;
            if (UseLogicalNameReferencing)
            {
                GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.Aarq, 0, buff, null, 0xff, Command.None);
                reply = GXDLMS.GetLnMessages(p);
            }
            else
            {
                reply = GXDLMS.GetSnMessages(new GXDLMSSNParameters(Settings, Command.Aarq, 0, 0, null, buff));
            }
            return reply;
        }

        /// <summary>
        /// Parses the AARE response.
        /// </summary>
        /// <param name="reply"></param>
        /// <remarks>
        /// Parse method will update the following data:
        /// <ul>
        /// <li>DLMSVersion</li>
        /// <li>MaxReceivePDUSize</li>
        /// <li>UseLogicalNameReferencing</li>
        /// <li>LNSettings or SNSettings</li>
        /// </ul>
        /// LNSettings or SNSettings will be updated, depending on the referencing,
        /// Logical name or Short name.
        /// </remarks>
        /// <returns>The AARE response</returns>
        /// <seealso cref="AARQRequest"/>
        /// <seealso cref="UseLogicalNameReferencing"/>
        /// <seealso cref="DLMSVersion"/>
        /// <seealso cref="MaxReceivePDUSize"/>
        public void ParseAAREResponse(GXByteBuffer reply)
        {
            try
            {
                IsAuthenticationRequired = (SourceDiagnostic)GXAPDU.ParsePDU(Settings, Settings.Cipher, reply, null) == SourceDiagnostic.AuthenticationRequired;
                if (IsAuthenticationRequired)
                {
                    System.Diagnostics.Debug.WriteLine("Authentication is required.");
                }
                else
                {
                    Settings.Connected |= ConnectionState.Dlms;
                }
                System.Diagnostics.Debug.WriteLine("- Server max PDU size is " + MaxReceivePDUSize);
                if (DLMSVersion != 6)
                {
                    throw new GXDLMSException("Invalid DLMS version number.");
                }
            }
            catch (OutOfMemoryException)
            {
                throw new Exception("Frame is not fully received.");
            }
        }

        /// <summary>
        /// Is authentication Required.
        /// </summary>
        /// <seealso cref="GetApplicationAssociationRequest"/>
        /// <seealso cref="ParseApplicationAssociationResponse"/>
        public bool IsAuthenticationRequired
        {
            get;
            private set;
        }

        /// <summary>
        /// Get challenge request if HLS authentication is used.
        /// </summary>
        /// <returns></returns>
        /// <seealso cref="IsAuthenticationRequired"/>
        /// <seealso cref="ParseApplicationAssociationResponse"/>
        public byte[][] GetApplicationAssociationRequest()
        {
            return GetApplicationAssociationRequest(null);
        }

        /// <summary>
        /// Get challenge request if HLS authentication is used.
        /// </summary>
        /// <returns></returns>
        /// <seealso cref="IsAuthenticationRequired"/>
        /// <seealso cref="ParseApplicationAssociationResponse"/>
        public byte[][] GetApplicationAssociationRequest(string ln)
        {
            if (Settings.Authentication != Authentication.HighECDSA &&
                Settings.Authentication != Authentication.HighGMAC &&
                    (Settings.Password == null || Settings.Password.Length == 0))
            {
                throw new ArgumentException("Password is invalid.");
            }
            Settings.ResetBlockIndex();
            byte[] pw;
            if (Settings.Authentication == Enums.Authentication.HighGMAC)
            {
                pw = Settings.Cipher.SystemTitle;
            }
            else if (Settings.Authentication == Enums.Authentication.HighSHA256)
            {
                GXByteBuffer tmp = new GXByteBuffer();
                tmp.Set(Settings.Password);
                tmp.Set(Settings.Cipher.SystemTitle);
                tmp.Set(Settings.SourceSystemTitle);
                tmp.Set(Settings.StoCChallenge);
                tmp.Set(Settings.CtoSChallenge);
                pw = tmp.Array();
            }
            else
            {
                pw = Settings.Password;
            }
            UInt32 ic = 0;
            if (Settings.Cipher != null)
            {
                ic = Settings.Cipher.InvocationCounter++;
            }
            byte[] challenge = GXSecure.Secure(Settings, Settings.Cipher, ic,
                                               Settings.StoCChallenge, pw);
            if (UseLogicalNameReferencing)
            {
                if (string.IsNullOrEmpty(ln))
                {
                    ln = "0.0.40.0.0.255";
                }
                return Method(ln, ObjectType.AssociationLogicalName,
                              1, challenge, DataType.OctetString);
            }
            return Method(0xFA00, ObjectType.AssociationShortName, 8, challenge,
                          DataType.OctetString);
        }

        /// <summary>
        /// Parse server's challenge if HLS authentication is used.
        /// </summary>
        /// <param name="reply"></param>
        /// <seealso cref="IsAuthenticationRequired"/>
        /// <seealso cref="GetApplicationAssociationRequest"/>
        public void ParseApplicationAssociationResponse(GXByteBuffer reply)
        {
            GXDataInfo info = new GXDataInfo();
            bool equals = false;
            byte[] value = (byte[])GXCommon.GetData(Settings, reply, info);
            if (value != null)
            {
                byte[] secret;
                UInt32 ic = 0;
                if (Settings.Authentication == Authentication.HighGMAC)
                {
                    secret = Settings.SourceSystemTitle;
                    GXByteBuffer bb = new GXByteBuffer(value);
                    bb.GetUInt8();
                    ic = bb.GetUInt32();
                }
                else if (Settings.Authentication == Enums.Authentication.HighSHA256)
                {
                    GXByteBuffer tmp2 = new GXByteBuffer();
                    tmp2.Set(Settings.Password);
                    tmp2.Set(Settings.SourceSystemTitle);
                    tmp2.Set(Settings.Cipher.SystemTitle);
                    tmp2.Set(Settings.CtoSChallenge);
                    tmp2.Set(Settings.StoCChallenge);
                    secret = tmp2.Array();
                }
                else
                {
                    secret = Settings.Password;
                }
                byte[] tmp = GXSecure.Secure(Settings, Settings.Cipher, ic,
                                             Settings.CtoSChallenge, secret);
                GXByteBuffer challenge = new GXByteBuffer(tmp);
                equals = challenge.Compare(value);
                Settings.Connected |= ConnectionState.Dlms;
            }
            if (!equals)
            {
                Settings.Connected &= ~ConnectionState.Dlms;
                throw new GXDLMSException("Invalid password. Server to Client challenge do not match.");
            }
        }

        /// <summary>
        /// The version can be used for backward compatibility.
        /// </summary>
        public int Version
        {
            get
            {
                return Settings.Version;
            }
            set
            {
                Settings.Version = value;
            }
        }

        /// <summary>
        /// If protected release is used release is including a ciphered xDLMS Initiate request.
        /// </summary>
        public bool UseProtectedRelease
        {
            get;
            set;
        }

        /// <summary>
        /// Generates a release request.
        /// </summary>
        /// <returns>Release request, as byte array.</returns>
        public byte[][] ReleaseRequest()
        {
            // If connection is not established, there is no need to send
            // DisconnectRequest.
            if ((Settings.Connected & ConnectionState.Dlms) == 0)
            {
                return null;
            }
            GXByteBuffer buff = new GXByteBuffer();
            if (!UseProtectedRelease)
            {
                buff.SetUInt8(3);
                buff.SetUInt8(0x80);
                buff.SetUInt8(1);
                buff.SetUInt8(0);
            }
            else
            {
                //Length.
                buff.SetUInt8(0);
                buff.SetUInt8(0x80);
                buff.SetUInt8(01);
                buff.SetUInt8(00);
                //Increase IC.
                if (Settings.Cipher != null && Settings.Cipher.IsCiphered())
                {
                    ++Settings.Cipher.InvocationCounter;
                }
                GXAPDU.GenerateUserInformation(Settings, Settings.Cipher, null, buff);
                buff.SetUInt8(0, (byte)(buff.Size - 1));
            }
            byte[][] reply;
            if (UseLogicalNameReferencing)
            {
                GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.ReleaseRequest, 0, buff, null, 0xff, Command.None);
                reply = GXDLMS.GetLnMessages(p);
            }
            else
            {
                reply = GXDLMS.GetSnMessages(new GXDLMSSNParameters(Settings, Command.ReleaseRequest, 0xFF, 0xFF, null, buff));
            }
            Settings.Connected &= ~ConnectionState.Dlms;
            return reply;
        }
        /// <summary>
        /// Generates a disconnect request.
        /// </summary>
        /// <returns>Disconnected request, as byte array.</returns>
        public byte[] DisconnectRequest()
        {
            return DisconnectRequest(false);
        }

        /// <summary>
        /// Generates a disconnect request.
        /// </summary>
        /// <returns>Disconnected request, as byte array.</returns>
        public byte[] DisconnectRequest(bool force)
        {
            if (!force && Settings.Connected == ConnectionState.None)
            {
                return null;
            }
            byte[] ret = null;
            if (Settings.InterfaceType == InterfaceType.HDLC)
            {
                ret = GXDLMS.GetHdlcFrame(Settings, (byte)Command.DisconnectRequest, null);
            }
            else if (force || Settings.Connected == ConnectionState.Dlms)
            {
                ret = ReleaseRequest()[0];
            }
            //Reset to max PDU size when connection is closed.
            Settings.MaxPduSize = 0xFFFF;
            Settings.Connected = ConnectionState.None;
            Settings.ResetFrameSequence();
            return ret;
        }

        /// <summary>
        /// Returns object types.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// This can be used with serialization.
        /// </remarks>
        public static Type[] GetObjectTypes()
        {
            return GXDLMS.GetObjectTypes(AvailableObjectTypes);
        }

        /// <summary>
        /// Returns object types.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// This can be used with serialization.
        /// </remarks>
        public static ObjectType[] GetObjectTypes2()
        {
            return GXDLMS.GetObjectTypes2(AvailableObjectTypes);
        }

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        /// <param name="ClassID"></param>
        /// <param name="Version"></param>
        /// <param name="BaseName"></param>
        /// <param name="LN"></param>
        /// <param name="AccessRights"></param>
        /// <returns></returns>
        internal static GXDLMSObject CreateDLMSObject(int ClassID, object Version, int BaseName, object LN, object AccessRights)
        {
            ObjectType type = (ObjectType)ClassID;
            GXDLMSObject obj = GXDLMS.CreateObject(type, AvailableObjectTypes);
            UpdateObjectData(obj, type, Version, BaseName, LN, AccessRights);
            return obj;
        }

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        GXDLMSObjectCollection ParseSNObjects(GXByteBuffer buff, bool onlyKnownObjects)
        {
            //Get array tag.
            byte size = buff.GetUInt8();
            //Check that data is in the array
            if (size != 0x01)
            {
                throw new GXDLMSException("Invalid response.");
            }
            GXDLMSObjectCollection items = new GXDLMSObjectCollection(this);
            long cnt = GXCommon.GetObjectCount(buff);
            GXDataInfo info = new GXDataInfo();
            for (long objPos = 0; objPos != cnt; ++objPos)
            {
                // Some meters give wrong item count.
                if (buff.Position == buff.Size)
                {
                    break;
                }
                object tmp = GXCommon.GetData(Settings, buff, info);
                List<object> objects;
                if (tmp is List<object>)
                {
                    objects = (List<object>)tmp;
                }
                else
                {
                    objects = new List<object>((object[])tmp);
                }
                info.Clear();
                if (objects.Count != 4)
                {
                    throw new GXDLMSException("Invalid structure format.");
                }
                int ot = Convert.ToUInt16(objects[1]);
                int baseName = Convert.ToInt32(objects[0]) & 0xFFFF;
                if (!onlyKnownObjects || AvailableObjectTypes.ContainsKey((ObjectType)ot))
                {
                    if (baseName > 0)
                    {
                        GXDLMSObject comp = CreateDLMSObject(ot, objects[2], baseName, objects[3], null);
                        if (comp != null)
                        {
                            items.Add(comp);
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Unknown object : {0} {1}", ot, baseName));
                }
            }
            return items;
        }

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        /// <param name="objectType"></param>
        /// <param name="version"></param>
        /// <param name="baseName"></param>
        /// <param name="logicalName"></param>
        /// <param name="accessRights"></param>
        internal static void UpdateObjectData(
            GXDLMSObject obj,
            ObjectType objectType,
            object version,
            object baseName,
            object logicalName,
            object accessRights)
        {
            obj.ObjectType = objectType;
            // Check access rights...
            if (accessRights is List<object> && ((List<object>)accessRights).Count == 2)
            {
                //access_rights: access_right
                List<object> access = (List<object>)accessRights;
                foreach (List<object> attributeAccess in (List<object>)access[0])
                {
                    int id = Convert.ToInt32(attributeAccess[0]);
                    AccessMode mode = (AccessMode)Convert.ToInt32(attributeAccess[1]);
                    //With some meters id is negative.
                    if (id > 0)
                    {
                        obj.SetAccess(id, mode);
                    }
                }
                if (((List<object>)access[1]).Count != 0)
                {
                    if (((List<object>)access[1])[0] is List<object>)
                    {
                        foreach (List<object> methodAccess in (List<object>)access[1])
                        {
                            int id = Convert.ToInt32(methodAccess[0]);
                            int tmp;
                            //If version is 0.
                            if (methodAccess[1] is Boolean)
                            {
                                tmp = ((Boolean)methodAccess[1]) ? 1 : 0;
                            }
                            else//If version is 1.
                            {
                                tmp = Convert.ToInt32(methodAccess[1]);
                            }
                            if (id > 0)
                            {
                                obj.SetMethodAccess(id, (MethodAccessMode)tmp);
                            }
                        }
                    }
                    else //All versions from Actaris SL 7000 do not return collection as standard says.
                    {
                        List<object> arr = (List<object>)access[1];
                        int id = Convert.ToInt32(arr[0]) + 1;
                        int tmp;
                        //If version is 0.
                        if (arr[1] is Boolean)
                        {
                            tmp = ((Boolean)arr[1]) ? 1 : 0;
                        }
                        else//If version is 1.
                        {
                            tmp = Convert.ToInt32(arr[1]);
                        }
                        obj.SetMethodAccess(id, (MethodAccessMode)tmp);
                    }
                }
            }
            if (baseName != null)
            {
                obj.ShortName = Convert.ToUInt16(baseName);
            }
            if (version != null)
            {
                obj.Version = Convert.ToInt32(version);
            }
            if (logicalName is byte[])
            {
                obj.LogicalName = GXCommon.ToLogicalName((byte[])logicalName);
            }
            else
            {
                obj.LogicalName = logicalName.ToString();
            }
        }

        /// <summary>
        /// Available objects.
        /// </summary>
        public GXDLMSObjectCollection Objects
        {
            get
            {
                return Settings.Objects;
            }
        }

        /// <summary>
        /// Parses the COSEM objects of the received data.
        /// </summary>
        /// <param name="data">Received data, from the device, as byte array. </param>
        /// <returns>Collection of COSEM objects.</returns>
        public GXDLMSObjectCollection ParseObjects(
        GXByteBuffer data,
        bool onlyKnownObjects)
        {
            if (data == null || data.Size == 0)
            {
                throw new GXDLMSException("ParseObjects failed. Invalid parameter.");
            }
            GXDLMSObjectCollection objects = null;
            if (UseLogicalNameReferencing)
            {
                objects = ParseLNObjects(data, onlyKnownObjects);
            }
            else
            {
                objects = ParseSNObjects(data, onlyKnownObjects);
            }
            if (CustomObisCodes != null)
            {
                foreach (GXObisCode it in CustomObisCodes)
                {
                    if (it.Append)
                    {
                        GXDLMSObject obj = GXDLMSClient.CreateObject(it.ObjectType);
                        obj.Version = it.Version;
                        obj.LogicalName = it.LogicalName;
                        obj.Description = it.Description;
                        objects.Add(obj);
                    }
                }
            }
            GXDLMSConverter c = new GXDLMSConverter(Standard);
            c.UpdateOBISCodeInformation(objects);
            Settings.Objects = objects;
            Settings.Objects.Parent = this;
            return objects;
        }

        /// <summary>
        /// Returns collection of push objects.
        /// </summary>
        /// <param name="data">Received data.</param>
        /// <returns>Array of objects and called indexes.</returns>
        public List<KeyValuePair<GXDLMSObject, int>> ParsePushObjects(List<object> data)
        {
            List<KeyValuePair<GXDLMSObject, int>> objects = new List<KeyValuePair<GXDLMSObject, int>>();
            if (data != null)
            {
                GXDLMSConverter c = new GXDLMSConverter(Standard);
                foreach (object tmp in data)
                {
                    List<object> it;
                    if (tmp is List<object>)
                    {
                        it = (List<object>)tmp;
                    }
                    else
                    {
                        it = new List<object>((object[])tmp);
                    }
                    int classID = ((UInt16)(it[0])) & 0xFFFF;
                    if (classID > 0)
                    {
                        GXDLMSObject comp;
                        comp = this.Objects.FindByLN((ObjectType)classID, GXCommon.ToLogicalName(it[1] as byte[]));
                        if (comp == null)
                        {
                            comp = GXDLMSClient.CreateDLMSObject(classID, 0, 0, it[1], null);
                            c.UpdateOBISCodeInformation(comp);
                        }
                        if ((comp is IGXDLMSBase))
                        {
                            objects.Add(new KeyValuePair<GXDLMSObject, int>(comp, (sbyte)it[2]));
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Unknown object : {0} {1}",
                                classID, GXCommon.ToLogicalName((byte[])it[1])));
                        }
                    }
                }
            }
            return objects;
        }

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        GXDLMSObjectCollection ParseLNObjects(
        GXByteBuffer buff,
        bool onlyKnownObjects)
        {
            byte size = buff.GetUInt8();
            //Check that data is in the array.
            if (size != 0x01)
            {
                throw new GXDLMSException("Invalid response.");
            }
            //get object count
            int cnt = GXCommon.GetObjectCount(buff);
            int objectCnt = 0;
            GXDLMSObjectCollection items = new GXDLMSObjectCollection(this);
            GXDataInfo info = new GXDataInfo();
            //Some meters give wrong item count.
            while (buff.Position != buff.Size && cnt != objectCnt)
            {
                info.Clear();
                object tmp = GXCommon.GetData(Settings, buff, info);
                List<object> objects;
                if (tmp is List<object>)
                {
                    objects = (List<object>)tmp;
                }
                else
                {
                    objects = new List<object>((object[])tmp);
                }
                if (objects.Count != 4)
                {
                    throw new GXDLMSException("Invalid structure format.");
                }
                ++objectCnt;
                int ot = Convert.ToInt16(objects[0]);
                if (!onlyKnownObjects || AvailableObjectTypes.ContainsKey((ObjectType)ot))
                {
                    GXDLMSObject comp = CreateDLMSObject(ot, objects[1], 0, objects[2], objects[3]);
                    items.Add(comp);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Unknown object : {0} {1}", ot, GXCommon.ToLogicalName((byte[])objects[2])));
                }
            }
            return items;
        }


        /// <summary>
        /// Get Value from byte array received from the meter.
        /// </summary>
        public object UpdateValue(
            GXDLMSObject target,
            int attributeIndex,
            object value)
        {
            return UpdateValue(target, attributeIndex, value, null);
        }

        /// <summary>
        /// Get Value from byte array received from the meter.
        /// </summary>
        public object UpdateValue(
            GXDLMSObject target,
            int attributeIndex,
            object value,
            List<GXKeyValuePair<GXDLMSObject, GXDLMSCaptureObject>> columns)
        {
            //Update data type if value is readable.
            if (value != null)
            {
                try
                {
                    target.SetDataType(attributeIndex, GXCommon.GetDLMSDataType(value.GetType()));
                }
                catch (Exception)
                {
                    //It's ok if this fails.
                }
            }
            if (value is byte[])
            {
                DataType type = target.GetUIDataType(attributeIndex);
                if (type != DataType.None)
                {
                    if (type == DataType.DateTime && ((byte[])value).Length == 5)
                    {
                        type = DataType.Date;
                        target.SetUIDataType(attributeIndex, type);
                    }
                    value = ChangeType((byte[])value, type, UtcTimeZone);
                }
            }
            ValueEventArgs e = new ValueEventArgs(Settings, target, attributeIndex, 0, columns);
            e.Value = value;
            (target as IGXDLMSBase).SetValue(Settings, e);
            target.ClearDirty(e.Index);
            return target.GetValues()[attributeIndex - 1];
        }

        /// <summary>
        /// Update list values.
        /// </summary>
        /// <param name="list">COSEM objects to update.</param>
        /// <param name="values">Received values.</param>
        public void UpdateValues(List<KeyValuePair<GXDLMSObject, int>> list, List<object> values)
        {
            int pos = 0;
            foreach (KeyValuePair<GXDLMSObject, int> it in list)
            {
                ValueEventArgs e = new ValueEventArgs(Settings, it.Key, it.Value, 0, null);
                e.Value = values[pos];
                (it.Key as IGXDLMSBase).SetValue(Settings, e);
                ++pos;
            }
        }

        /// <summary>
        /// Parse access response.
        /// </summary>
        /// <param name="list">Collection of access items.</param>
        /// <param name="data">Received data from the meter.</param>
        /// <returns>Collection of received data and status codes.</returns>
        /// <seealso cref="AccessRequest"/>
        public List<KeyValuePair<object, ErrorCode>> ParseAccessResponse(
            List<GXDLMSAccessItem> list,
            GXByteBuffer data)
        {
            int pos;
            //Get count
            GXDataInfo info = new GXDataInfo();
            int cnt = GXCommon.GetObjectCount(data);
            if (list.Count != cnt)
            {
                throw new ArgumentOutOfRangeException("List size and values size do not match.");
            }
            List<object> values = new List<object>(cnt);
            List<KeyValuePair<object, ErrorCode>> reply = new List<KeyValuePair<object, ErrorCode>>(cnt);
            for (pos = 0; pos != cnt; ++pos)
            {
                info.Clear();
                Object value = GXCommon.GetData(Settings, data, info);
                values.Add(value);
            }
            //Get status codes.
            cnt = GXCommon.GetObjectCount(data);
            if (values.Count != cnt)
            {
                throw new ArgumentOutOfRangeException("List size and values size do not match.");
            }
            foreach (object it in values)
            {
                //Get access type.
                data.GetUInt8();
                //Get status.
                reply.Add(new KeyValuePair<object, ErrorCode>(it, (ErrorCode)data.GetUInt8()));
            }
            pos = 0;
            foreach (GXDLMSAccessItem it in list)
            {
                if (it.Command == AccessServiceCommandType.Get && reply[pos].Value == ErrorCode.Ok)
                {
                    ValueEventArgs ve = new ValueEventArgs(Settings, it.Target, it.Index, 0, null);
                    ve.Value = values[pos];
                    (it.Target as IGXDLMSBase).SetValue(Settings, ve);
                }
                ++pos;
            }
            return reply;
        }

        public static GXDLMSAttributeSettings GetAttributeInfo(
            GXDLMSObject item,
            int index)
        {
            GXDLMSAttributeSettings att = item.Attributes.Find(index);
            return att;
        }


        /// <summary>
        /// Changes byte array received from the meter to given type.
        /// </summary>
        /// <param name="value">Byte array received from the meter.</param>
        /// <param name="type">Wanted type.</param>
        /// <returns>Value changed by type.</returns>
        public static object ChangeType(byte[] value, DataType type)
        {
            return ChangeType(new GXByteBuffer(value), type, false);
        }

        /// <summary>
        /// Changes byte array received from the meter to given type.
        /// </summary>
        /// <param name="value">Byte array received from the meter.</param>
        /// <param name="type">Wanted type.</param>
        /// <param name="useUtc">Standard says that Time zone is from normal time to UTC in minutes.
        /// If meter is configured to use UTC time (UTC to normal time) set this to true.</param>
        /// <returns>Value changed by type.</returns>
        public static object ChangeType(byte[] value, DataType type, bool useUtc)
        {
            return ChangeType(new GXByteBuffer(value), type, useUtc);
        }

        /// <summary>
        /// Changes byte array received from the meter to given type.
        /// </summary>
        /// <param name="value">Byte array received from the meter.</param>
        /// <param name="type">Wanted type.</param>
        /// <returns>Value changed by type.</returns>
        public object ChangeType(GXByteBuffer value, DataType type)
        {
            return ChangeType(value, type, false);
        }

        /// <summary>
        /// Changes byte array received from the meter to given type.
        /// </summary>
        /// <param name="value">Byte array received from the meter.</param>
        /// <param name="type">Wanted type.</param>
        /// <param name="useUtc">Standard says that Time zone is from normal time to UTC in minutes.
        /// If meter is configured to use UTC time (UTC to normal time) set this to true.</param>
        /// <returns>Value changed by type.</returns>
        public static object ChangeType(GXByteBuffer value, DataType type, bool useUtc)
        {
            if ((value == null || value.Size == 0) && (type == DataType.String || type == DataType.OctetString))
            {
                return string.Empty;
            }
            if (value == null)
            {
                return null;
            }
            if (type == DataType.None)
            {
                return BitConverter.ToString(value.Data, value.Position, value.Size - value.Position).Replace('-', ' ');
            }
            if (value.Size == 0
                    && (type == DataType.String || type == DataType.OctetString))
            {
                return "";
            }
            if (value.Size == 0 && type == DataType.DateTime)
            {
                return new GXDateTime(DateTime.MinValue);
            }
            if (value.Size == 0 && type == DataType.Date)
            {
                return new GXDate(DateTime.MinValue);
            }
            if (value.Size == 0 && type == DataType.Time)
            {
                return new GXTime(DateTime.MinValue);
            }

            GXDataInfo info = new GXDataInfo();
            info.Type = type;
            GXDLMSSettings settings = new GXDLMSSettings(false);
            settings.UseUtc2NormalTime = useUtc;
            Object ret = GXCommon.GetData(settings, value, info);
            if (!info.Complete)
            {
                throw new OutOfMemoryException();
            }
            if (type == DataType.OctetString && ret is byte[])
            {
                String str;
                byte[] arr = (byte[])ret;
                if (arr.Length == 0)
                {
                    str = "";
                }
                else
                {
                    StringBuilder bcd = new StringBuilder(arr.Length * 4);
                    foreach (int it in arr)
                    {
                        if (bcd.Length != 0)
                        {
                            bcd.Append(".");
                        }
                        bcd.Append(it.ToString());
                    }
                    str = bcd.ToString();
                }
                return str;
            }
            return ret;
        }

        /// <summary>
        /// Reads the selected object from the device.
        /// </summary>
        /// <remarks>
        /// This method is used to get all registers in the device.
        /// </remarks>
        /// <returns>Read request, as byte array.</returns>
        public byte[] GetObjectsRequest()
        {
            return GetObjectsRequest(null);
        }

        /// <summary>
        /// Reads the selected object from the device.
        /// </summary>
        /// <remarks>
        /// This method is used to get all registers in the device.
        /// </remarks>
        /// <returns>Read request, as byte array.</returns>
        public byte[] GetObjectsRequest(string ln)
        {
            object name;
            Settings.ResetBlockIndex();
            if (UseLogicalNameReferencing)
            {
                if (string.IsNullOrEmpty(ln))
                {
                    name = "0.0.40.0.0.255";
                }
                else
                {
                    name = ln;
                }
            }
            else
            {
                name = (ushort)0xFA00;
            }
            return Read(name, ObjectType.AssociationLogicalName, 2)[0];
        }

        /// <summary>
        /// Generate Method (Action) request.
        /// </summary>
        /// <param name="item">object to write.</param>
        /// <param name="index">Attribute index where data is write.</param>
        /// <returns></returns>
        public byte[][] Method(GXDLMSObject item, int index, Object data)
        {
            return Method(item.Name, item.ObjectType, index, data, DataType.None);
        }

        /// <summary>
        /// Generate Method (Action) request.
        /// </summary>
        /// <param name="item">object to write.</param>
        /// <param name="index">Attribute index where data is write.</param>
        /// <param name="data">Additional data.</param>
        /// <param name="type">Additional data type.</param>
        /// <returns></returns>
        public byte[][] Method(GXDLMSObject item, int index, Object data, DataType type)
        {
            return Method(item.Name, item.ObjectType, index, data, type);
        }

        /// <summary>
        /// Generate Method (Action) request.
        /// </summary>
        /// <param name="name">Method object short name or Logical Name.</param>
        /// <param name="objectType">Object type.</param>
        /// <param name="index">Method index.</param>
        /// <param name="value">Additional data.</param>
        /// <param name="type">Additional data type.</param>
        /// <returns></returns>
        public byte[][] Method(object name, ObjectType objectType, int index, Object value, DataType type)
        {
            if (name == null || (index < 1 && Standard != Standard.Italy))
            {
                throw new ArgumentOutOfRangeException("Invalid parameter");
            }
            Settings.ResetBlockIndex();
            if (AutoIncreaseInvokeID)
            {
                Settings.InvokeID = (byte)((Settings.InvokeID + 1) & 0xF);
            }
            if (type == DataType.None && value != null)
            {
                type = GXDLMSConverter.GetDLMSDataType(value);
            }
            GXByteBuffer attributeDescriptor = new GXByteBuffer();
            GXByteBuffer data = new GXByteBuffer();
            GXCommon.SetData(Settings, data, type, value);
            if (UseLogicalNameReferencing)
            {
                // CI
                attributeDescriptor.SetUInt16((UInt16)objectType);
                // Add LN
                attributeDescriptor.Set(GXCommon.LogicalNameToBytes((string)name));
                // Attribute ID.
                attributeDescriptor.SetUInt8((byte)index);
                // Method Invocation Parameters is not used.
                if (type == DataType.None)
                {
                    attributeDescriptor.SetUInt8(0);
                }
                else
                {
                    attributeDescriptor.SetUInt8(1);
                }
                GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.MethodRequest, (byte)ActionRequestType.Normal, attributeDescriptor, data, 0xff, Command.None);
                return GXDLMS.GetLnMessages(p);
            }
            else
            {
                byte requestType;
                if (type == DataType.None)
                {
                    requestType = (byte)VariableAccessSpecification.VariableName;
                }
                else
                {
                    requestType = (byte)VariableAccessSpecification.ParameterisedAccess;
                }
                int ind, count;
                GXDLMS.GetActionInfo(objectType, out ind, out count);
                if (index > count)
                {
                    throw new ArgumentException("methodIndex");
                }
                UInt16 sn = Convert.ToUInt16(name);
                index = (ind + (index - 1) * 0x8);
                sn += (UInt16)index;
                // Add name.
                attributeDescriptor.SetUInt16(sn);
                // Add selector.
                if (type != DataType.None)
                {
                    attributeDescriptor.SetUInt8(1);
                }

                return GXDLMS.GetSnMessages(new GXDLMSSNParameters(Settings, Command.ReadRequest, 1, requestType, attributeDescriptor, data));
            }
        }

        /// <summary>
        /// Generates a write message.
        /// </summary>
        /// <param name="item">object to write.</param>
        /// <param name="index">Attribute index where data is write.</param>
        /// <returns></returns>
        public byte[][] Write(
            GXDLMSObject item,
            int index)
        {
            if (item == null || index < 1)
            {
                throw new GXDLMSException("Invalid parameter");
            }
            Object value = (item as IGXDLMSBase).GetValue(Settings, new ValueEventArgs(Settings, item, index, 0, null));
            DataType type = item.GetDataType(index);
            if (type == DataType.None)
            {
                type = GXDLMSConverter.GetDLMSDataType(value);
            }
            //If values is show as string, but send as byte array.
            if (value is string && type == DataType.OctetString)
            {
                DataType tp = item.GetUIDataType(index);
                if (tp == DataType.String)
                {
                    value = ASCIIEncoding.ASCII.GetBytes((string)value);
                }
            }
            return Write2(item.Name, value, type, item.ObjectType, index);
        }

        /// <summary>
        /// Generates a write message.
        /// </summary>
        /// <param name="name">Short or Logical Name.</param>
        /// <param name="value">Data to Write.</param>
        /// <param name="type">Data type of write object.</param>
        /// <param name="objectType"></param>
        /// <param name="index">Attribute index where data is write.</param>
        /// <returns>Generated write frames.</returns>
        [Obsolete("Use Write")]
        public byte[][] Write(object name, object value, DataType type, ObjectType objectType, int index)
        {
            return Write2(name, value, type, objectType, index);
        }

        private byte[][] Write2(object name, object value, DataType type, ObjectType objectType, int index)
        {
            Settings.ResetBlockIndex();
            if (AutoIncreaseInvokeID)
            {
                Settings.InvokeID = (byte)((Settings.InvokeID + 1) & 0xF);
            }
            if (type == DataType.None && value != null)
            {
                type = GXDLMSConverter.GetDLMSDataType(value);
                if (type == DataType.None)
                {
                    throw new ArgumentException("Invalid parameter. Unknown value type.");
                }
            }
            GXByteBuffer attributeDescriptor = new GXByteBuffer();
            GXByteBuffer data = new GXByteBuffer();
            byte[][] reply;
            GXCommon.SetData(Settings, data, type, value);
            if (UseLogicalNameReferencing)
            {
                // Add CI.
                attributeDescriptor.SetUInt16((UInt16)objectType);
                // Add LN.
                attributeDescriptor.Set(GXCommon.LogicalNameToBytes((string)name));
                // Attribute ID.
                attributeDescriptor.SetUInt8((byte)index);
                // Access selection is not used.
                attributeDescriptor.SetUInt8(0);
                GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.SetRequest, (byte)SetRequestType.Normal, attributeDescriptor, data, 0xff, Command.None);
                p.blockIndex = Settings.BlockIndex;
                p.blockNumberAck = Settings.BlockNumberAck;
                p.Streaming = false;
                reply = GXDLMS.GetLnMessages(p);
            }
            else
            {
                // Add name.
                UInt16 sn = Convert.ToUInt16(name);
                sn += (UInt16)((index - 1) * 8);
                attributeDescriptor.SetUInt16(sn);
                //Data cnt.
                attributeDescriptor.SetUInt8(1);
                GXDLMSSNParameters p = new GXDLMSSNParameters(Settings, Command.WriteRequest, 1,
                        (byte)VariableAccessSpecification.VariableName, attributeDescriptor, data);
                reply = GXDLMS.GetSnMessages(p);
            }
            return reply;
        }

        /// <summary>
        /// Generates a read message.
        /// </summary>
        /// <param name="name">Short or Logical Name.</param>
        /// <param name="objectType">Read Interface.</param>
        /// <param name="attributeOrdinal">Read attribute index.</param>
        /// <returns>Read request as byte array.</returns>
        public byte[][] Read(object name, ObjectType objectType, int attributeOrdinal)
        {
            return Read(name, objectType, attributeOrdinal, null);
        }

        private byte[][] Read(object name, ObjectType objectType, int attributeOrdinal, GXByteBuffer data)
        {
            if ((attributeOrdinal < 0))
            {
                throw new ArgumentException("Invalid parameter");
            }
            Settings.ResetBlockIndex();
            if (AutoIncreaseInvokeID)
            {
                Settings.InvokeID = (byte)((Settings.InvokeID + 1) & 0xF);
            }
            GXByteBuffer attributeDescriptor = new GXByteBuffer();
            byte[][] reply;
            if (UseLogicalNameReferencing)
            {
                // CI
                attributeDescriptor.SetUInt16((UInt16)objectType);
                // Add LN
                attributeDescriptor.Set(GXCommon.LogicalNameToBytes((string)name));
                // Attribute ID.
                attributeDescriptor.SetUInt8((byte)attributeOrdinal);
                if (data == null || data.Size == 0)
                {
                    // Access selection is not used.
                    attributeDescriptor.SetUInt8(0);
                }
                else
                {
                    // Access selection is used.
                    attributeDescriptor.SetUInt8(1);
                }
                GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.GetRequest, (byte)GetCommandType.Normal, attributeDescriptor, data, 0xff, Command.None);
                reply = GXDLMS.GetLnMessages(p);
            }
            else
            {
                byte requestType;
                UInt16 sn = Convert.ToUInt16(name);
                sn += (UInt16)((attributeOrdinal - 1) * 8);
                attributeDescriptor.SetUInt16(sn);
                // parameterized-access
                if (data != null && data.Size != 0)
                {
                    requestType = (byte)VariableAccessSpecification.ParameterisedAccess;
                }
                else //variable-name
                {
                    requestType = (byte)VariableAccessSpecification.VariableName;
                }
                GXDLMSSNParameters p = new GXDLMSSNParameters(Settings, Command.ReadRequest, 1, requestType, attributeDescriptor, data);
                reply = GXDLMS.GetSnMessages(p);
            }
            return reply;
        }

        /// <summary>
        /// Generates a read message.
        /// </summary>
        /// <param name="item">DLMS object to read.</param>
        /// <param name="attributeOrdinal">Read attribute index.</param>
        /// <returns>Read request as byte array.</returns>
        public byte[][] Read(GXDLMSObject item, int attributeOrdinal)
        {
            return Read(item.Name, item.ObjectType, attributeOrdinal, null);
        }

        /// <summary>
        /// Read list of COSEM objects.
        /// </summary>
        /// <param name="list">List of COSEM object and attribute index to read.</param>
        /// <returns>Read List request as byte array.</returns>
        public byte[][] ReadList(List<KeyValuePair<GXDLMSObject, int>> list)
        {
            if ((NegotiatedConformance & Conformance.MultipleReferences) == 0)
            {
                throw new ArgumentOutOfRangeException("Meter doesn't support multiple objects reading with one request.");
            }
            if (list == null || list.Count == 0)
            {
                throw new ArgumentOutOfRangeException("Invalid parameter.");
            }
            Settings.ResetBlockIndex();
            List<byte[]> messages = new List<byte[]>();
            GXByteBuffer data = new GXByteBuffer();
            if (this.UseLogicalNameReferencing)
            {
                GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.GetRequest, (byte)GetCommandType.WithList, null, data, 0xff, Command.None);
                //Request service primitive shall always fit in a single APDU.
                int pos = 0, count = (Settings.MaxPduSize - 12) / 10;
                if (list.Count < count)
                {
                    count = list.Count;
                }
                //All meters can handle 10 items.
                if (count > 10)
                {
                    count = 10;
                }
                // Add length.
                GXCommon.SetObjectCount(count, data);
                foreach (KeyValuePair<GXDLMSObject, int> it in list)
                {
                    // CI.
                    data.SetUInt16((UInt16)it.Key.ObjectType);
                    String[] items = it.Key.LogicalName.Split('.');
                    if (items.Length != 6)
                    {
                        throw new ArgumentException("Invalid Logical Name.");
                    }
                    foreach (String it2 in items)
                    {
                        data.SetUInt8(byte.Parse(it2));
                    }
                    // Attribute ID.
                    data.SetUInt8((byte)it.Value);
                    // Attribute selector is not used.
                    data.SetUInt8(0);
                    ++pos;
                    if (pos % count == 0 && list.Count != pos)
                    {
                        messages.AddRange(GXDLMS.GetLnMessages(p));
                        data.Clear();
                        if (list.Count - pos < count)
                        {
                            GXCommon.SetObjectCount(list.Count - pos, data);
                        }
                        else
                        {
                            GXCommon.SetObjectCount(count, data);
                        }
                    }
                }
                messages.AddRange(GXDLMS.GetLnMessages(p));
            }
            else
            {
                GXDLMSSNParameters p = new GXDLMSSNParameters(Settings, Command.ReadRequest, list.Count, 0xFF, null, data);
                foreach (KeyValuePair<GXDLMSObject, int> it in list)
                {
                    // Add variable type.
                    data.SetUInt8(VariableAccessSpecification.VariableName);
                    int sn = it.Key.ShortName;
                    sn += (it.Value - 1) * 8;
                    data.SetUInt16((UInt16)sn);
                    if (data.Size >= Settings.MaxPduSize)
                    {
                        messages.AddRange(GXDLMS.GetSnMessages(p));
                        data.Clear();
                    }
                }
                messages.AddRange(GXDLMS.GetSnMessages(p));
            }
            return messages.ToArray();
        }

        /// <summary>
        /// Write list of COSEM objects.
        /// </summary>
        /// <param name="list">List of COSEM object and attribute index to read.</param>
        /// <returns>Write List request as byte array.</returns>
        public byte[][] WriteList(List<KeyValuePair<GXDLMSObject, int>> list)
        {
            if ((NegotiatedConformance & Conformance.MultipleReferences) == 0)
            {
                throw new ArgumentOutOfRangeException("Meter doesn't support multiple objects writing with one request.");
            }
            if (list == null || list.Count == 0)
            {
                throw new ArgumentOutOfRangeException("Invalid parameter.");
            }
            Settings.ResetBlockIndex();
            if (AutoIncreaseInvokeID)
            {
                Settings.InvokeID = (byte)((Settings.InvokeID + 1) & 0xF);
            }
            List<byte[]> messages = new List<byte[]>();
            GXByteBuffer data = new GXByteBuffer();
            if (this.UseLogicalNameReferencing)
            {
                GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.SetRequest, (byte)SetCommandType.WithList, null, data, 0xff, Command.None);
                // Add length.
                GXCommon.SetObjectCount(list.Count, data);
                foreach (KeyValuePair<GXDLMSObject, int> it in list)
                {
                    // CI.
                    data.SetUInt16((UInt16)it.Key.ObjectType);
                    data.Set(GXCommon.LogicalNameToBytes(it.Key.LogicalName));
                    // Attribute ID.
                    data.SetUInt8((byte)it.Value);
                    // Attribute selector is not used.
                    data.SetUInt8(0);
                }
                // Add length.
                GXCommon.SetObjectCount(list.Count, data);
                foreach (KeyValuePair<GXDLMSObject, int> it in list)
                {
                    Object value = (it.Key as IGXDLMSBase).GetValue(Settings, new ValueEventArgs(Settings, it.Key, it.Value, 0, null));
                    DataType type = it.Key.GetDataType(it.Value);
                    if (type == DataType.None)
                    {
                        type = GXDLMSConverter.GetDLMSDataType(value);
                    }
                    //If values is show as string, but send as byte array.
                    if (value is string && type == DataType.OctetString)
                    {
                        DataType tp = it.Key.GetUIDataType(it.Value);
                        if (tp == DataType.String)
                        {
                            value = ASCIIEncoding.ASCII.GetBytes((string)value);
                        }
                    }
                    GXCommon.SetData(Settings, data, type, value);
                }
                messages.AddRange(GXDLMS.GetLnMessages(p));
            }
            else
            {
                GXDLMSSNParameters p = new GXDLMSSNParameters(Settings, Command.WriteRequest, list.Count, 0xFF, null, data);
                foreach (KeyValuePair<GXDLMSObject, int> it in list)
                {
                    // Add variable type.
                    data.SetUInt8(VariableAccessSpecification.VariableName);
                    int sn = it.Key.ShortName;
                    sn += (it.Value - 1) * 8;
                    data.SetUInt16((UInt16)sn);
                }
                // Add length.
                GXCommon.SetObjectCount(list.Count, data);
                p.count = list.Count;
                foreach (KeyValuePair<GXDLMSObject, int> it in list)
                {
                    Object value = (it.Key as IGXDLMSBase).GetValue(Settings, new ValueEventArgs(Settings, it.Key, it.Value, 0, null));
                    DataType type = it.Key.GetDataType(it.Value);
                    if (type == DataType.None)
                    {
                        type = GXDLMSConverter.GetDLMSDataType(value);
                    }
                    //If values is show as string, but send as byte array.
                    if (value is string && type == DataType.OctetString)
                    {
                        DataType tp = it.Key.GetUIDataType(it.Value);
                        if (tp == DataType.String)
                        {
                            value = ASCIIEncoding.ASCII.GetBytes((string)value);
                        }
                    }
                    GXCommon.SetData(Settings, data, type, value);
                }
                messages.AddRange(GXDLMS.GetSnMessages(p));
            }
            return messages.ToArray();
        }

        /// <summary>
        /// Generates the keep alive message.
        /// </summary>
        /// <remarks>
        /// Keepalive message is needed to keep the connection up. Connection is closed if keepalive is not sent in meter's inactivity timeout period.
        /// </remarks>
        /// <returns>Returns Keep alive message, as byte array.</returns>
        public byte[] GetKeepAlive()
        {
            Settings.ResetBlockIndex();
            if (UseLogicalNameReferencing)
            {
                return Read("0.0.40.0.0.255", ObjectType.AssociationLogicalName, 1)[0];
            }
            return Read(0xFA00, ObjectType.AssociationShortName, 1)[0];
        }

        /// <summary>
        /// Read rows by entry.
        /// </summary>
        /// <remarks>
        /// Check Conformance because all meters do not support this.
        /// </remarks>
        /// <param name="pg">Profile generic object to read.</param>
        /// <param name="index">One based start index.</param>
        /// <param name="count">Rows count to read.</param>
        /// <returns>Read message as byte array.</returns>
        public byte[][] ReadRowsByEntry(GXDLMSProfileGeneric pg, UInt32 index, UInt32 count)
        {
            return ReadRowsByEntry(pg, index, count, null);
        }

        /// <summary>
        /// Read rows by entry.
        /// </summary>
        /// <remarks>
        /// Check Conformance because all meters do not support this.
        /// </remarks>
        /// <param name="pg">Profile generic object to read.</param>
        /// <param name="index">One based start index.</param>
        /// <param name="count">Rows count to read.</param>
        /// <param name="columns">Columns to read.</param>
        /// <returns>Read message as byte array.</returns>
        public byte[][] ReadRowsByEntry(GXDLMSProfileGeneric pg, UInt32 index, UInt32 count,
                                        List<GXKeyValuePair<GXDLMSObject, GXDLMSCaptureObject>> columns)
        {
            int columnIndex = 1;
            int columnEnd = 0;
            int pos = 0;
            // If columns are given find indexes.
            if (columns != null && columns.Count != 0)
            {
                if (pg.CaptureObjects == null || pg.CaptureObjects.Count == 0)
                {
                    throw new Exception("Read capture objects first.");
                }
                columnIndex = pg.CaptureObjects.Count;
                columnEnd = 1;
                foreach (GXKeyValuePair<GXDLMSObject, GXDLMSCaptureObject> c in columns)
                {
                    pos = 0;
                    bool found = false;
                    foreach (GXKeyValuePair<GXDLMSObject, GXDLMSCaptureObject> it in pg.CaptureObjects)
                    {
                        ++pos;
                        if (it.Key.ObjectType == c.Key.ObjectType
                                && it.Key.LogicalName.CompareTo(c.Key.LogicalName) == 0
                                && it.Value.AttributeIndex == c.Value.AttributeIndex
                                && it.Value.DataIndex == c.Value.DataIndex)
                        {
                            found = true;
                            if (pos < columnIndex)
                            {
                                columnIndex = pos;
                            }
                            columnEnd = pos;
                            break;
                        }
                    }
                    if (!found)
                    {
                        throw new Exception("Invalid column: " + c.Key.LogicalName);
                    }
                }
            }
            return ReadRowsByEntry(pg, index, count, columnIndex, columnEnd);
        }

        /// <summary>
        /// Read rows by entry.
        /// </summary>
        /// <remarks>
        /// Check Conformance because all meters do not support this.
        /// </remarks>
        /// <param name="pg">Profile generic object to read.</param>
        /// <param name="index">One based start index.</param>
        /// <param name="count">Rows count to read.</param>
        /// <param name="columnStart">One based column start index.</param>
        /// <param name="columnEnd">Column end index.</param>
        /// <returns>Read message as byte array.</returns>
        public byte[][] ReadRowsByEntry(GXDLMSProfileGeneric pg, UInt32 index, UInt32 count, int columnStart, int columnEnd)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            if (columnStart < 1)
            {
                throw new ArgumentOutOfRangeException("columnStart");
            }
            if (columnEnd < 0)
            {
                throw new ArgumentOutOfRangeException("columnEnd");
            }
            pg.Reset();
            Settings.ResetBlockIndex();
            GXByteBuffer buff = new GXByteBuffer(19);
            // Add AccessSelector value
            buff.SetUInt8(0x02);
            // Add enum tag.
            buff.SetUInt8((byte)DataType.Structure);
            // Add item count
            buff.SetUInt8(0x04);
            // Add start index
            GXCommon.SetData(Settings, buff, DataType.UInt32, index);
            // Add Count
            if (count == 0)
            {
                GXCommon.SetData(Settings, buff, DataType.UInt32, count);
            }
            else
            {
                GXCommon.SetData(Settings, buff, DataType.UInt32, index + count - 1);
            }
            // Select columns to read.
            GXCommon.SetData(Settings, buff, DataType.UInt16, columnStart);
            GXCommon.SetData(Settings, buff, DataType.UInt16, columnEnd);
            return Read(pg.Name, ObjectType.ProfileGeneric, 2, buff);
        }

        /// <summary>
        /// Read rows by range.
        /// </summary>
        /// <remarks>
        /// Use this method to read Profile Generic table between dates.
        /// Check Conformance because all meters do not support this.
        /// Some meters return error if there are no data betweens start and end time.
        /// </remarks>
        /// <param name="pg">Profile generic object to read.</param>
        /// <param name="start">Start time.</param>
        /// <param name="end">End time.</param>
        /// <returns>Read message as byte array.</returns>
        public byte[][] ReadRowsByRange(GXDLMSProfileGeneric pg, DateTime start, DateTime end)
        {
            return ReadRowsByRange(pg, start, end, null);
        }

        /// <summary>
        /// Read rows by range.
        /// </summary>
        /// <remarks>
        /// Use this method to read Profile Generic table between dates.
        /// Check Conformance because all meters do not support this.
        /// Some meters return error if there are no data betweens start and end time.
        /// </remarks>
        /// <param name="pg">Profile generic object to read.</param>
        /// <param name="start">Start time.</param>
        /// <param name="end">End time.</param>
        /// <returns>Read message as byte array.</returns>
        public byte[][] ReadRowsByRange(GXDLMSProfileGeneric pg, GXDateTime start, GXDateTime end)
        {
            return ReadRowsByRange(pg, start, end, null);
        }

        /// <summary>
        /// Read rows by range.
        /// </summary>
        /// <remarks>
        /// Use this method to read Profile Generic table between dates.
        /// Check Conformance because all meters do not support this.
        /// Some meters return error if there are no data betweens start and end time.
        /// </remarks>
        /// <param name="pg">Profile generic object to read.</param>
        /// <param name="start">Start time.</param>
        /// <param name="end">End time.</param>
        /// <param name="columns">Columns to read.</param>
        /// <returns>Read message as byte array.</returns>
        public byte[][] ReadRowsByRange(GXDLMSProfileGeneric pg, DateTime start, DateTime end,
                                        List<GXKeyValuePair<GXDLMSObject, GXDLMSCaptureObject>> columns)
        {
            return ReadRowsByRange(pg, new GXDateTime(start), new GXDateTime(end), columns);
        }

        /// <summary>
        /// Read rows by range.
        /// </summary>
        /// <remarks>
        /// Use this method to read Profile Generic table between dates.
        /// Check Conformance because all meters do not support this.
        /// Some meters return error if there are no data betweens start and end time.
        /// </remarks>
        /// <param name="pg">Profile generic object to read.</param>
        /// <param name="start">Start time.</param>
        /// <param name="end">End time.</param>
        /// <param name="columns">Columns to read.</param>
        /// <returns>Read message as byte array.</returns>
        public byte[][] ReadRowsByRange(GXDLMSProfileGeneric pg, GXDateTime start, GXDateTime end,
                                        List<GXKeyValuePair<GXDLMSObject, GXDLMSCaptureObject>> columns)
        {
            pg.Reset();
            Settings.ResetBlockIndex();
            string ln = "0.0.1.0.0.255";
            ObjectType type = ObjectType.Clock;
            int index = 2;
            bool unixTime = false;
            //If Unix time is used.
            if (pg.CaptureObjects.Count != 0 && pg.CaptureObjects[0].Key is GXDLMSData &&
                pg.CaptureObjects[0].Key.LogicalName == "0.0.1.1.0.255")
            {
                unixTime = true;
                ln = "0.0.1.1.0.255";
                type = ObjectType.Data;
            }
            GXByteBuffer buff = new GXByteBuffer(51);
            // Add AccessSelector value.
            buff.SetUInt8(0x01);
            // Add enum tag.
            buff.SetUInt8((byte)DataType.Structure);
            // Add item count
            buff.SetUInt8(0x04);
            // Add enum tag.
            buff.SetUInt8((byte)DataType.Structure);
            // Add item count
            buff.SetUInt8(0x04);
            // CI
            GXCommon.SetData(Settings, buff, DataType.UInt16,
                             type);
            // LN
            GXCommon.SetData(Settings, buff, DataType.OctetString, GXCommon.LogicalNameToBytes(ln));
            // Add attribute index.
            GXCommon.SetData(Settings, buff, DataType.Int8, index);
            // Add version.
            GXCommon.SetData(Settings, buff, DataType.UInt16, 0);
            if (unixTime)
            {
                // Add start time.
                GXCommon.SetData(Settings, buff, DataType.UInt32, GXDateTime.ToUnixTime(start));
                // Add end time.
                GXCommon.SetData(Settings, buff, DataType.UInt32, GXDateTime.ToUnixTime(end));
            }
            else
            {
                // Add start time.
                GXCommon.SetData(Settings, buff, DataType.OctetString, start);
                // Add end time.
                GXCommon.SetData(Settings, buff, DataType.OctetString, end);
            }

            // Add array of read columns.
            buff.SetUInt8(DataType.Array);
            if (columns == null)
            {
                // Add item count
                buff.SetUInt8(0x00);
            }
            else
            {
                GXCommon.SetObjectCount(columns.Count, buff);
                foreach (GXKeyValuePair<GXDLMSObject, GXDLMSCaptureObject> it in columns)
                {
                    buff.SetUInt8(DataType.Structure);
                    // Add items count.
                    buff.SetUInt8(4);
                    // CI
                    GXCommon.SetData(Settings, buff, DataType.UInt16, it.Key.ObjectType);
                    // LN
                    GXCommon.SetData(Settings, buff, DataType.OctetString, GXCommon.LogicalNameToBytes(it.Key.LogicalName));
                    // Add attribute index.
                    GXCommon.SetData(Settings, buff, DataType.Int8, it.Value.AttributeIndex);
                    // Add data index.
                    GXCommon.SetData(Settings, buff, DataType.Int16, it.Value.DataIndex);
                }
            }
            return Read(pg.Name, ObjectType.ProfileGeneric, 2, buff);
        }

        /// <summary>
        /// Create given type of COSEM object.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static GXDLMSObject CreateObject(ObjectType type)
        {
            return GXDLMS.CreateObject(type, AvailableObjectTypes);
        }

        /// <summary>
        /// Generates an acknowledgment message, with which the server is informed to
        /// send next packets.
        /// </summary>
        /// <param name="type">Frame type</param>
        /// <returns>Acknowledgment message as byte array.</returns>
        public byte[] ReceiverReady(RequestTypes type)
        {
            return GXDLMS.ReceiverReady(Settings, type);
        }

        /// <summary>
        /// Generates an acknowledgment message, with which the server is informed to
        /// send next packets.
        /// </summary>
        /// <param name="reply">Reply data.</param>
        /// <returns>Acknowledgment message as byte array.</returns>
        public byte[] ReceiverReady(GXReplyData reply)
        {
            return GXDLMS.ReceiverReady(Settings, reply);
        }

        ///<summary>Removes the frame from the packet, and returns DLMS PDU.</summary>
        ///<param name="reply">The received data from the device.</param>
        ///<param name="data">Information from the received data.</param>
        ///<returns>Is frame complete.</returns>
        [Obsolete("Use GetData with notify parameter instead.")]
        public bool GetData(byte[] reply, GXReplyData data)
        {
            return GetData(new GXByteBuffer(reply), data);
        }

        ///<summary>Removes the frame from the packet, and returns DLMS PDU.</summary>
        ///<param name="reply">The received data from the device.</param>
        ///<param name="data">Information from the received data.</param>
        ///<returns>Is frame complete.</returns>
        public bool GetData(byte[] reply, GXReplyData data, GXReplyData notify)
        {
            return GetData(new GXByteBuffer(reply), data, notify);
        }

        ///<summary>
        ///Removes the HDLC frame from the packet, and returns COSEM data only.
        ///</summary>
        ///<param name="reply">
        ///The received data from the device.
        ///</param>
        ///<param name="data">
        ///The exported reply information.
        ///</param>
        ///<returns>
        /// Is frame complete.
        ///</returns>
        [Obsolete("Use GetData with notify parameter instead.")]
        public virtual bool GetData(GXByteBuffer reply, GXReplyData data)
        {
            return GetData(reply, data, null);
        }

        ///<summary>
        ///Removes the HDLC frame from the packet, and returns COSEM data only.
        ///</summary>
        ///<param name="reply">The received data from the device.</param>
        ///<param name="data">The exported reply information.</param>
        ///<param name="notify">Optional notify data.</param>
        ///<returns>Is frame complete.</returns>
        public virtual bool GetData(GXByteBuffer reply, GXReplyData data, GXReplyData notify)
        {
            data.Xml = null;
            bool ret = false;
            Command cmd = data.Command;
            try
            {
                ret = GXDLMS.GetData(Settings, reply, data, notify, this);
            }
            catch (Exception ex)
            {
                if (translator == null || throwExceptions)
                {
                    throw ex;
                }
                ret = true;
            }
            if (ret && translator != null && data.MoreData == RequestTypes.None)
            {
                if (data.Xml == null)
                {
                    data.Xml = new GXDLMSTranslatorStructure(translator.OutputType, translator.OmitXmlNameSpace, translator.Hex, translator.ShowStringAsHex, translator.Comments, translator.tags);
                }
                int pos = data.Data.Position;
                try
                {
                    GXByteBuffer data2 = data.Data;
                    if (data.Command == Command.GetResponse)
                    {
                        GXByteBuffer tmp = new GXByteBuffer((UInt16)(4 + data.Data.Size));
                        tmp.SetUInt8(data.Command);
                        tmp.SetUInt8(GetCommandType.Normal);
                        tmp.SetUInt8((byte)data.InvokeId);
                        tmp.SetUInt8(0);
                        tmp.Set(data.Data);
                        data.Data = tmp;
                    }
                    else if (data.Command == Command.MethodResponse)
                    {
                        GXByteBuffer tmp = new GXByteBuffer((UInt16)(6 + data.Data.Size));
                        tmp.SetUInt8(data.Command);
                        tmp.SetUInt8(GetCommandType.Normal);
                        tmp.SetUInt8((byte)data.InvokeId);
                        tmp.SetUInt8(0);
                        tmp.SetUInt8(1);
                        tmp.SetUInt8(0);
                        tmp.Set(data.Data);
                        data.Data = tmp;
                    }
                    else if (data.Command == Command.ReadResponse)
                    {
                        GXByteBuffer tmp = new GXByteBuffer((UInt16)(3 + data.Data.Size));
                        tmp.SetUInt8(data.Command);
                        tmp.SetUInt8(VariableAccessSpecification.VariableName);
                        tmp.SetUInt8((byte)data.InvokeId);
                        tmp.SetUInt8(0);
                        tmp.Set(data.Data);
                        data.Data = tmp;
                    }
                    data.Data.Position = 0;
                    if (data.Command == Command.Snrm || data.Command == Command.Ua)
                    {
                        data.Xml.AppendStartTag(data.Command);
                        if (data.Data.Size != 0)
                        {
                            translator.PduToXml(data.Xml, data.Data, translator.OmitXmlDeclaration, translator.OmitXmlNameSpace, true);
                        }
                        data.Xml.AppendEndTag(data.Command);
                    }
                    else
                    {
                        if (data.Data.Size != 0)
                        {
                            translator.PduToXml(data.Xml, data.Data, translator.OmitXmlDeclaration, translator.OmitXmlNameSpace, true);
                        }
                        data.Data = data2;
                    }
                }
                finally
                {
                    data.Data.Position = pos;
                }
            }
            return ret;
        }

        /// <summary>
        /// Get value from DLMS byte stream.
        /// </summary>
        /// <param name="data">Received data.</param>
        /// <returns>Parsed value.</returns>
        public static object GetValue(GXByteBuffer data)
        {
            return GetValue(data, DataType.None, false);
        }


        /// <summary>
        /// Get value from DLMS byte stream.
        /// </summary>
        /// <param name="data">Received data.</param>
        /// <param name="useUtc">Standard says that Time zone is from normal time to UTC in minutes.
        /// If meter is configured to use UTC time (UTC to normal time) set this to true.</param>
        /// <returns>Parsed value.</returns>
        public static object GetValue(GXByteBuffer data, bool useUtc)
        {
            return GetValue(data, DataType.None, useUtc);
        }

        /// <summary>
        /// Get value from DLMS byte stream.
        /// </summary>
        /// <param name="data">Received data.</param>
        /// <param name="type">Conversion type is used if returned data is byte array.</param>
        /// <returns>Parsed value.</returns>
        public static object GetValue(GXByteBuffer data, DataType type)
        {
            return GetValue(data, type, false);
        }

        /// <summary>
        /// Get value from DLMS byte stream.
        /// </summary>
        /// <param name="data">Received data.</param>
        /// <param name="type">Conversion type is used if returned data is byte array.</param>
        /// <param name="useUtc">Standard says that Time zone is from normal time to UTC in minutes.
        /// If meter is configured to use UTC time (UTC to normal time) set this to true.</param>
        /// <returns>Parsed value.</returns>
        public static object GetValue(GXByteBuffer data, DataType type, bool useUtc)
        {
            GXDataInfo info = new GXDataInfo();
            object value = GXCommon.GetData(null, data, info);
            if (value is byte[] && type != DataType.None)
            {
                value = GXDLMSClient.ChangeType((byte[])value, type, useUtc);
            }
            return value;
        }

        /// <summary>
        /// Convert physical address and logical address to server address.
        /// </summary>
        /// <param name="logicalAddress">Server logical address.</param>
        /// <param name="physicalAddress">Server physical address.</param>
        /// <returns>Server address.</returns>
        public static int GetServerAddress(int logicalAddress, int physicalAddress)
        {
            return GetServerAddress(logicalAddress, physicalAddress, 0);
        }

        /// <summary>
        /// Convert physical address and logical address to server address.
        /// </summary>
        /// <param name="logicalAddress">Server logical address.</param>
        /// <param name="physicalAddress">Server physical address.</param>
        /// <param name="addressSize">Address size in bytes.</param>
        /// <returns>Server address.</returns>
        public static int GetServerAddress(int logicalAddress, int physicalAddress, int addressSize)
        {
            int value;
            if (addressSize < 4 && physicalAddress < 0x80 && logicalAddress < 0x80)
            {
                value = logicalAddress << 7 | physicalAddress;
            }
            else if (physicalAddress < 0x4000 && logicalAddress < 0x4000)
            {
                value = logicalAddress << 14 | physicalAddress;
            }
            else
            {
                throw new ArgumentException("Invalid logical or physical address.");
            }
            return value;
        }

        /// <summary>
        /// Converts meter serial number to server address.
        /// Default formula is used.
        /// </summary>
        /// <remarks>
        /// All meters do not use standard formula or support serial number addressing at all.
        /// </remarks>
        /// <param name="serialNumber">Meter serial number.</param>
        /// <returns>Server address.</returns>
        public static int GetServerAddress(int serialNumber)
        {
            return GetServerAddress(serialNumber, null);
        }
        /// <summary>
        /// Converts meter serial number to server address.
        /// </summary>
        /// <param name="serialNumber">Meter serial number.</param>
        /// <param name="formula">Formula used to convert serial number to server address.</param>
        /// <returns>Server address.</returns>
        /// <remarks>
        /// All meters do not use standard formula or support serial number addressing at all.
        /// </remarks>
        public static int GetServerAddress(int serialNumber, string formula)
        {
            //If formula is not given use default formula.
            //This formula is defined in DLMS specification.
            if (String.IsNullOrEmpty(formula))
            {
                formula = "SN % 10000 + 1000";
            }
            return 0x4000 | SerialnumberCounter.Count(serialNumber, formula);
        }

        /// <summary>
        /// Generates a access service message.
        /// </summary>
        /// <param name="time">Send time. Set to DateTime.MinValue is not used.</param>
        /// <param name="list"></param>
        /// <returns>Access request as byte array.</returns>
        /// <seealso cref="ParseAccessResponse"/>
        public byte[][] AccessRequest(DateTime time, List<GXDLMSAccessItem> list)
        {
            GXByteBuffer bb = new GXByteBuffer();
            GXCommon.SetObjectCount(list.Count, bb);
            foreach (GXDLMSAccessItem it in list)
            {
                bb.SetUInt8(it.Command);
                //Object type.
                bb.SetUInt16((ushort)it.Target.ObjectType);
                //LN
                String[] items = it.Target.LogicalName.Split('.');
                if (items.Length != 6)
                {
                    throw new ArgumentException("Invalid Logical Name.");
                }
                foreach (String it2 in items)
                {
                    bb.SetUInt8(byte.Parse(it2));
                }
                // Attribute ID.
                bb.SetUInt8(it.Index);
            }
            //Data
            GXCommon.SetObjectCount(list.Count, bb);
            foreach (GXDLMSAccessItem it in list)
            {
                if (it.Command == AccessServiceCommandType.Get)
                {
                    bb.SetUInt8(0);
                }
                else
                {
                    object value = (it.Target as IGXDLMSBase).GetValue(Settings, new ValueEventArgs(it.Target, it.Index, 0, null));
                    DataType type = it.Target.GetDataType(it.Index);
                    if (type == DataType.None)
                    {
                        type = GXDLMSConverter.GetDLMSDataType(value);
                    }
                    GXCommon.SetData(Settings, bb, type, value);
                }
            }

            GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, Command.AccessRequest, 0xFF, null, bb, 0xff, Command.None);
            p.time = new GXDateTime(time);
            return GXDLMS.GetLnMessages(p);
        }

        /// <summary>
        /// Get initial Conformance
        /// </summary>
        /// <param name="useLogicalNameReferencing">Is logical name referencing used.</param>
        /// <returns>Initial Conformance.</returns>
        public static Conformance GetInitialConformance(bool useLogicalNameReferencing)
        {
            if (useLogicalNameReferencing)
            {
                return Conformance.BlockTransferWithAction |
                           Conformance.BlockTransferWithSetOrWrite |
                           Conformance.BlockTransferWithGetOrRead |
                           Conformance.Set | Conformance.SelectiveAccess |
                           Conformance.Action | Conformance.MultipleReferences |
                           Conformance.Get;
            }
            return Conformance.InformationReport |
                        Conformance.Read | Conformance.UnconfirmedWrite |
                        Conformance.Write | Conformance.ParameterizedAccess |
                        Conformance.MultipleReferences;
        }

        /// <summary>
        /// Parse received Information reports and Event notifications.
        /// </summary>
        /// <param name="reply">Reply.</param>
        /// <returns>Data notification data.</returns>
        public object ParseReport(GXReplyData reply, List<KeyValuePair<GXDLMSObject, int>> list)
        {
            if (reply.Command == Command.EventNotification)
            {
                GXDLMSLNCommandHandler.HandleEventNotification(Settings, reply, list);
                return null;
            }
            else if (reply.Command == Command.InformationReport)
            {
                GXDLMSSNCommandHandler.HandleInformationReport(Settings, reply, list);
                return null;
            }
            else if (reply.Command == Command.DataNotification)
            {
                return reply.Value;
            }
            else
            {
                throw new ArgumentException("Invalid command. " + reply.Command);
            }
        }

        /// <summary>
        /// Generates a invalid HDLC frame.
        /// </summary>
        /// <param name="command">HDLC command.</param>
        /// <param name="data">data</param>
        /// <returns>HDLC frame request, as byte array.</returns>
        /// <remarks>
        /// This method can be used for sending custom HDLC frames example in testing.
        /// </remarks>
        public byte[] CustomHdlcFrameRequest(byte command, GXByteBuffer data)
        {
            if (Settings.InterfaceType != InterfaceType.HDLC)
            {
                throw new Exception("This method can be used only to generate HDLC custom frames");
            }
            return GXDLMS.GetHdlcFrame(Settings, command, data);
        }

        /// <summary>
        /// Generates a invalid HDLC frame.
        /// </summary>
        /// <param name="command">HDLC command.</param>
        /// <param name="data">data</param>
        /// <returns>HDLC frame request, as byte array.</returns>
        /// <remarks>
        /// This method can be used for sending custom HDLC frames example in testing.
        /// </remarks>
        public byte[][] CustomFrameRequest(Command command, GXByteBuffer data)
        {
            if (Settings.InterfaceType == InterfaceType.HDLC ||
                Settings.InterfaceType == InterfaceType.WRAPPER)
            {
                byte[][] reply;
                if (command == Command.None)
                {
                    List<byte[]> messages = new List<byte[]>();
                    if (Settings.InterfaceType == Enums.InterfaceType.WRAPPER)
                    {
                        messages.Add(GXDLMS.GetWrapperFrame(Settings, data));
                    }
                    else if (Settings.InterfaceType == Enums.InterfaceType.HDLC)
                    {
                        messages.Add(GXDLMS.GetHdlcFrame(Settings, (byte)command, data));
                    }
                    reply = messages.ToArray();
                }
                else if (UseLogicalNameReferencing)
                {
                    GXDLMSLNParameters p = new GXDLMSLNParameters(this, Settings, 0, command, 0, data, null, 0xff, Command.None);
                    reply = GXDLMS.GetLnMessages(p);
                }
                else
                {
                    reply = GXDLMS.GetSnMessages(new GXDLMSSNParameters(Settings, command, 0, 0, null, data));
                }
                return reply;
            }
            throw new Exception("This method can be used only to generate HDLC or WRAPPER custom frames");
        }

        /// <summary>
        /// Get size of the frame.
        /// </summary>
        /// <remarks>
        /// When WRAPPER is used this method can be used to check how many bytes we need to read.
        /// </remarks>
        /// <param name="data">Received data.</param>
        /// <returns>Size of received bytes on the frame.</returns>
        public int GetFrameSize(GXByteBuffer data)
        {
            if (InterfaceType == InterfaceType.WRAPPER)
            {
                if (data.Available < 8 || data.GetUInt16(data.Position) != 1)
                {
                    return 8 - data.Available;
                }
                return data.GetUInt16(data.Position + 6);
            }
            return 1;
        }

        /// <summary>
        /// Get HDLC sender and receiver address information.
        /// </summary>
        /// <param name="reply">Received data.</param>
        /// <param name="target">target (primary) address</param>
        /// <param name="source">Source (secondary) address.</param>
        /// <param name="type">DLMS frame type.</param>
        public static void GetHdlcAddressInfo(GXByteBuffer reply, out int target, out int source, out byte type)
        {
            GXDLMS.GetHdlcAddressInfo(reply, out target, out source, out type);
        }
    }
}