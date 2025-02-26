using Microsoft.VisualStudio.TestTools.UnitTesting;
using HackPDM;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Collections;
using OClient = OdooRpcCs.OdooClient;


namespace HackPDMTests
{
	[TestClass()]
	public class OdooDefaultsTests
	{
		private List<string> modelList = null;
		[TestMethod()]
		public void GetOdooUser() => Assert.IsFalse( OdooDefaults.OdooUser is null || OdooDefaults.OdooUser == "" );
		[TestMethod()]
		public void GetOdooPass() => Assert.IsFalse( OdooDefaults.OdooPass is null || OdooDefaults.OdooPass == "" );
		[TestMethod()]
		public void GetOdooID()
		{
			Assert.IsFalse( OdooDefaults.OdooID == 0 );
		}
		[TestMethod()]
		public void GetOdooAddress()
		{
			using ( Ping pinger = new Ping() )
			{
				PingReply reply = pinger.Send(OdooDefaults.OdooAddress);
				Assert.IsTrue( reply.Status == IPStatus.Success );
			}
		}
		[TestMethod()]
		public void GetOdooPort()
		{
			new TcpClient(OdooDefaults.OdooAddress, int.Parse(OdooDefaults.OdooPort));
		}

		[TestMethod()]
		public void GetOdooModelHpNode()					=> ContainsModelTest( OdooDefaults.HP_NODE );
		[TestMethod()]
		public void GetOdooModelHpEntry()					=> ContainsModelTest( OdooDefaults.HP_ENTRY );
		[TestMethod()]
		public void GetOdooModelHpEntryNameFilter()			=> ContainsModelTest( OdooDefaults.HP_ENTRY_NAME_FILTER );
		[TestMethod()]
		public void GetOdooModelHpDirectory()				=> ContainsModelTest( OdooDefaults.HP_DIRECTORY );
		[TestMethod()]
		public void GetOdooModelHpCategory()				=> ContainsModelTest( OdooDefaults.HP_CATEGORY );
		[TestMethod()]
		public void GetOdooModelHpCategoryProperty()		=> ContainsModelTest( OdooDefaults.HP_CATEGORY_PROPERTY );
		[TestMethod()]
		public void GetOdooModelHpVersion()					=> ContainsModelTest( OdooDefaults.HP_VERSION );
		[TestMethod()]
		public void GetOdooModelHpVersionProperty()			=> ContainsModelTest( OdooDefaults.HP_VERSION_PROPERTY );
		[TestMethod()]
		public void GetOdooModelHpVersionRelationship()		=> ContainsModelTest( OdooDefaults.HP_VERSION_RELATIONSHIP );
		[TestMethod()]
		public void GetOdooModelHpRelease()					=> ContainsModelTest( OdooDefaults.HP_RELEASE );
		[TestMethod()]
		public void GetOdooModelHpReleaseVersionRel()		=> ContainsModelTest( OdooDefaults.HP_RELEASE_VERSION_REL );
		[TestMethod()]
		public void GetOdooModelHpProperty()				=> ContainsModelTest( OdooDefaults.HP_PROPERTY );
		[TestMethod()]
		public void GetOdooModelHpType()					=> ContainsModelTest( OdooDefaults.HP_TYPE );
		[TestMethod()]
		public void GetOdooModelResUsers()					=> ContainsModelTest( OdooDefaults.RES_USERS );
		[TestMethod()]
		public void GetOdooModelIrAttachment()				=> ContainsModelTest(OdooDefaults.IR_ATTACHMENT);
		
		// odoo xmlrpc 
		[TestMethod()]
		public void XmlRPC () => Assert.IsTrue( HasEntries() );





		// helper methods
		private void ContainsModelTest( string model ) => Assert.IsTrue(Contains(model));
		private bool Contains( string model )
		{
			if ( !( modelList is null ) ) return modelList.Contains( model );

			ArrayList filter = new ArrayList(){"model"};
			ArrayList records = OClient.Browse(OdooDefaults.IR_MODEL, new ArrayList(){new ArrayList(), filter});

			modelList = new List<string>();
			foreach ( Hashtable record in records )
			{
				modelList.Add( (string)record [ "model" ] );
			}

			return modelList.Contains( model );
		}
		private bool HasEntries()
		{
			// just need the Contains method to try to populate modelList
			Contains("-");
			return modelList != null && modelList.Count > 0;
		}
	}
}