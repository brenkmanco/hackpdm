/*
 * Created by SharpDevelop.
 * User: matt
 * Date: 9/29/2012
 * Time: 9:12 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Npgsql;
using Mono.Security.Protocol.Tls;
using Mono.Security.Authenticode;


namespace HackPDM
{
	/// <summary>
	/// Handle SSL Database Connections
	/// http://fxjr.blogspot.com/2010/04/using-ssl-client-certificates-with.html
	/// </summary>
	public class SqlConn
	{
		
		private static void SqlConnNew(string[] args)
		{
			string conStr =
			"Server=xxx.xxx.xxx.xxx;" +
			"User Id=xxx;" +
			"Password=xxx;" +
			"Protocol=3;" +
			"Database=xxx;" +
			"SSL=True;" +
			"Sslmode=Require;";
			
			NpgsqlConnection conn = new NpgsqlConnection(conStr);
			
			conn.ProvideClientCertificatesCallback += new ProvideClientCertificatesCallback(MyProvideClientCertificates);
			
			conn.CertificateSelectionCallback += new CertificateSelectionCallback(MyCertificateSelectionCallback);
			
			conn.CertificateValidationCallback += new CertificateValidationCallback(MyCertificateValidationCallback);
			
			conn.PrivateKeySelectionCallback += new PrivateKeySelectionCallback(MyPrivateKeySelectionCallback);
			
			try
			{
				conn.Open();
				System.Console.WriteLine("Verbindung aufgebaut");
			}
			catch (Exception e)
			{
				System.Console.WriteLine(e);
			}
			finally
			{
				conn.Close();
				System.Console.ReadLine();
			}
		}
		
		
		static void MyProvideClientCertificates(X509CertificateCollection clienteCertis)
		{
			X509Certificate cert = new X509Certificate("mycert.crt");
			clienteCertis.Add(cert);
		}
		
		
		static X509Certificate MyCertificateSelectionCallback(X509CertificateCollection clienteCertis, X509Certificate serverCerti, string hostDestino, X509CertificateCollection serverRequestedCertificates)
		{
			return clienteCertis[0];
		}
		
		static AsymmetricAlgorithm MyPrivateKeySelectionCallback(X509Certificate certificate, string targetHost)
		{
			PrivateKey key =null;
			try
			{
				//it is very important that the key has the .pvk format in windows!!!
				key = PrivateKey.CreateFromFile("myKey.pvk", "xxx");
			}
			catch (CryptographicException ex)
			{
				Console.WriteLine();
				Console.WriteLine();
				Console.WriteLine(ex);
				Console.WriteLine();
				Console.WriteLine();
			}
			
			if (key == null)
			return null;
			
			return key.RSA;
		}
	
	
	
		static bool MyCertificateValidationCallback(X509Certificate certificate, int[] certificateErrors)
		{
			/*
			* CertVALID = 0,
			* CertEXPIRED = -2146762495,//0x800B0101
			* CertVALIDITYPERIODNESTING = -2146762494,//0x800B0102
			* CertROLE = -2146762493,//0x800B0103
			* CertPATHLENCONST = -2146762492,//0x800B0104
			* CertCRITICAL = -2146762491,//0x800B0105
			* CertPURPOSE = -2146762490,//0x800B0106
			* CertISSUERCHAINING = -2146762489,//0x800B0107
			* CertMALFORMED = -2146762488,//0x800B0108
			* CertUNTRUSTEDROOT = -2146762487,//0x800B0109
			* CertCHAINING = -2146762486,//0x800B010A
			* CertREVOKED = -2146762485,//0x800B010C
			* CertUNTRUSTEDTESTROOT = -2146762484,//0x800B010D
			* CertREVOCATION_FAILURE = -2146762483,//0x800B010E
			* CertCN_NO_MATCH = -2146762482,//0x800B010F
			* CertWRONG_USAGE = -2146762481,//0x800B0110
			* CertUNTRUSTEDCA = -2146762480,//0x800B0112
			*/
			
			//error: -2146762487, -2146762481
			System.Console.WriteLine(certificateErrors[0]);
			return true;
		}
		
		
	}
}
