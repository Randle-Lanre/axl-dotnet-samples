﻿/* 
AXL addUser sample script, using DotNet Core WCF

Adds a simple end user, then deletes the user.

Copyright (c) 2020 Cisco and/or its affiliates.
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Text;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using System.Xml.Serialization;

using AXLClient;
using Debug;
using DotNetEnv;

namespace addUser
{
    class Program
    {
        static async Task Main( string[] args )
        {
            Console.WriteLine( "\nStarting up...\n" );

            // Load environment variables from .env
            DotNetEnv.Env.Load( "../../.env" );

            // Change to true to enable output of request/response headers and XML
            var DEBUG = System.Environment.GetEnvironmentVariable( "DEBUG" );

            // Create a custom binding so we can allow the client to use cookies with AXL
            BasicHttpsBinding binding = new BasicHttpsBinding();
            binding.AllowCookies = true;

            // Specify the CUCM AXL API location for the SOAP client
            EndpointAddress address = new EndpointAddress( $"https://{ System.Environment.GetEnvironmentVariable( "CUCM_ADDRESS" ) }:8443/axl/" );
            
            //Class generated from AXL WSDL
            AXLPortClient client = new AXLPortClient( binding, address );

            if ( DEBUG == "True" ) {
                client.Endpoint.EndpointBehaviors.Add( new DebugEndpointBehaviour() );
            }

            // To disable HTTPS certificate checking, uncomment the below lines
            // NOT for production use!  See README.md for AXL certificate install steps

            // client.ChannelFactory.Credentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication
            //     {
            //         CertificateValidationMode = X509CertificateValidationMode.None,
            //         RevocationMode = X509RevocationMode.NoCheck
            //     };
            // client.ChannelFactory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.None;
            // client.ClientCredentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.None;

            // Incantation to force alternate serializer reflection behaviour due to complexities in the AXL schema
            // See https://github.com/dotnet/wcf/issues/2219
            MethodInfo method = typeof( XmlSerializer ).GetMethod( "set_Mode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
            method.Invoke( null, new object[] { 1 } ); 

            // Base64 encode AXL username/password for Basic Auth
            var encodedUserPass = Convert.ToBase64String( Encoding.ASCII.GetBytes( 
                System.Environment.GetEnvironmentVariable( "CUCM_USERNAME" ) + ":" +
                System.Environment.GetEnvironmentVariable( "CUCM_PASSWORD" )
            ) );

            // Incantation to create and populate a Basic Auth HTTP header
            // This must be done to force SoapCore to include the Authorization header on the first attempt
            HttpRequestMessageProperty requestProperty = new HttpRequestMessageProperty();
            requestProperty.Headers[ "Authorization" ] = "Basic " + encodedUserPass;

            // Creating context block apparently allows attaching custom HTTP headers to the request
            var scope = new OperationContextScope( client.InnerChannel );
            OperationContext.Current.OutgoingMessageProperties[ HttpRequestMessageProperty.Name ] = requestProperty;

            //Create the request object
            AddUserReq addUserReq = new AddUserReq(); 
            
            addUserReq.user = new XUser();
            addUserReq.user.lastName = "TestUser";
            addUserReq.user.userid = "testUser";
            addUserReq.user.password = "Cisco!1234";

            string userPkid = "";

            //Try the addUser request
            try
                {
                    addUserResponse addUserResp = await client.addUserAsync( addUserReq );
                    userPkid = addUserResp.addUserResponse1.@return;
                }
            catch ( Exception ex )
                {
                    Console.WriteLine( $"\nError: addUser: { ex.Message }" );
                    Environment.Exit( -1 );
                }

            Console.WriteLine( $"addUser: SUCCESS  pkid: { userPkid }\n");

            Console.WriteLine( "Press Enter to continue..." );
            Console.ReadLine();

            RemoveUserReq removeUserReq = new RemoveUserReq();

            removeUserReq.ItemElementName = ItemChoiceType102.userid;
            removeUserReq.Item = "testUser";

            //Try the removeUser request
            try
                {
                    removeUserResponse removeUserResp = await client.removeUserAsync( removeUserReq );
                }
            catch ( Exception ex )
                {
                    Console.WriteLine( $"\nError: removeUser: { ex.Message }" );
                    Environment.Exit( -1 );
                }

            Console.WriteLine( "removeUser: SUCCESS" );

        }
    }
}
