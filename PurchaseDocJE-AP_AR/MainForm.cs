﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PurchaseDocJE_AP_AR
{
    public partial class MainForm : Form
    {
        private string authenticationToken = "";
        private string user = "";
        private string server;
        private string instance;
        private int tbPort;

        private List<Installment> installments = new List<Installment>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            Login aLog = new Login();
            if (aLog.ShowDialog() == DialogResult.OK)
            {
                authenticationToken = aLog.AuthenticationToken;
                user = aLog.User;
                server = aLog.Server;
                instance = aLog.Instance;
                tbPort = aLog.TbPort;

                if (tbPort == 0)
                {
                    rtbxResults.Text = $"Authentication token: {authenticationToken}";
                    lblConnectionInfo.Text = "Connected";
                }
                else
                {
                    lblConnectionInfo.Text = $"Connected, port {tbPort}";
                }
            }

        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Login.DoLogout(authenticationToken, server, instance);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            if (tbxXMLFile.Text != string.Empty)
            {
                dlg.InitialDirectory = Directory.GetParent(tbxXMLFile.Text).FullName;
            }
            else if (Properties.Settings.Default.StartFolder != string.Empty)
            {
                dlg.InitialDirectory = Properties.Settings.Default.StartFolder;
            }
            else
            {
                dlg.InitialDirectory = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).Parent.Parent.FullName, "samples");
            }

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                tbxXMLFile.Text = dlg.FileName;
                Properties.Settings.Default.StartFolder = Directory.GetParent(dlg.FileName).FullName;
            }
        }

        private void btnPostDocument_Click(object sender, EventArgs e)
        {
            if (authenticationToken == string.Empty)
            {
                MessageBox.Show("Please login before posting");
                return;
            }

            if (tbxXMLFile.Text == string.Empty)
            {
                MessageBox.Show("Please select an XML file to load");
                return;
            }

            using (MagoTBServices.TbServicesSoapClient aTbSvc = new MagoTBServices.TbServicesSoapClient())
            {
                aTbSvc.Endpoint.Address = new System.ServiceModel.EndpointAddress($"http://{server}/{instance}/TBServices/TBServices.asmx");

                XElement xmlDoc = XElement.Load(tbxXMLFile.Text);
                // remove comments from sample XML if any, SetData does not accept them
                xmlDoc.DescendantNodes().OfType<XComment>().Remove();
                string strResult;
                bool bSuccess = aTbSvc.SetData(authenticationToken, xmlDoc.ToString(), DateTime.Now, 0, true, out strResult);
                if (bSuccess)
                {
                    XElement xmlResult = XElement.Parse(strResult);

                    // the returned XML has a namespace, need to be matched for the XPath query below
                    XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
                    nsmgr.AddNamespace("maxs", xmlResult.Name.NamespaceName);

                    try
                    {
                        // The Id of the created Journal entry and other useful data are extracted
                        // from the returned XML, by matching the first node containing the corresponding tag
                        tbxJournalEntryID.Text = (((IEnumerable)xmlResult.XPathEvaluate("//maxs:JournalEntryId", nsmgr)).Cast<XElement>()).FirstOrDefault().Value;
                        tbxSupplierCode.Text = (((IEnumerable)xmlResult.XPathEvaluate("//maxs:CustSupp", nsmgr)).Cast<XElement>()).FirstOrDefault().Value;
                        tbxDocumentDate.Text = (((IEnumerable)xmlResult.XPathEvaluate("//maxs:DocumentDate", nsmgr)).Cast<XElement>()).FirstOrDefault().Value;
                        tbxDocumentNumber.Text = (((IEnumerable)xmlResult.XPathEvaluate("//maxs:DocNo", nsmgr)).Cast<XElement>()).FirstOrDefault().Value;
                        tbxTotalAmount.Text = (((IEnumerable)xmlResult.XPathEvaluate("//maxs:TotalAmount", nsmgr)).Cast<XElement>()).FirstOrDefault().Value;
                        tbxTaxAmount.Text = (((IEnumerable)xmlResult.XPathEvaluate("//maxs:TaxAmount", nsmgr)).Cast<XElement>()).FirstOrDefault().Value;

                        rtbxResults.Text = $"success \n{xmlResult}\n";
                    }
                    catch (Exception ex)
                    {
                        rtbxResults.Text = $"!!!FAILED!!! \n[{ex.Message}]\n";
                    }
                }
                else
                {
                    rtbxResults.Text = $"!!!FAILED!!! \n[{strResult}]\n";
                }
            }

        }

        private void btnInstallments_Click(object sender, EventArgs e)
        {
            if (authenticationToken == string.Empty)
            {
                MessageBox.Show("Please login before posting");
                return;
            }

            if (tbxInstPayment.Text == string.Empty || tbxInstStartDate.Text == string.Empty || tbxInstTotAmount.Text == string.Empty || tbxInstTaxAmount.Text == string.Empty)
            {
                MessageBox.Show("Please set a payment term code, a starting date, a total amount and a tax amount");
                return;
            }

            AP_AR_Components.AP_ARComponents AP_AR = new AP_AR_Components.AP_ARComponents();
            AP_AR.Url = string.Format("http://{0}:{1}/ERP.AP_AR.Components/AP_ARComponents", server, tbPort);

            AP_AR.HeaderInfo = new AP_AR_Components.TBHeaderInfo();
            AP_AR.HeaderInfo.AuthToken = authenticationToken;

            int handle = AP_AR.InstallmentDetails_Create();

            int InstNo = 0;
            try
            {
                bool bMore;
                installments.Clear();
                do
                {
                    Installment installment = new Installment();
                    bMore = AP_AR.InstallmentDetails_CalculateInstallmentData(
                        handle,
                        tbxInstPayment.Text,
                        "EUR",
                        tbxInstStartDate.Text,
                        double.Parse(tbxInstTotAmount.Text, CultureInfo.InvariantCulture),
                        double.Parse(tbxInstTaxAmount.Text, CultureInfo.InvariantCulture),
                        ref InstNo,
                        ref installment.Date,
                        ref installment.Type,
                        ref installment.Amount
                    );
                    if (bMore)
                    {
                        installments.Add(installment);
                        InstNo++;
                    }
                } while (bMore);

                rtbxResults.Text = string.Empty;
                foreach (var inst in installments)
                {
                    rtbxResults.Text += $"{inst.Date}  {inst.Amount}\n";
                }
            }
            catch (Exception ex)
            {
                rtbxResults.Text = $"!!!FAILED!!! \n[{ex.Message}]\n";
            }

            AP_AR.InstallmentDetails_Dispose(handle);
        }

        private void btnCreatePayable_Click(object sender, EventArgs e)
        {
            // omitting the PaymentTerm node will apply the default payment of the Supplier master
            XElement xmlPayable = XElement.Parse($@"<?xml version='1.0'?>
                <maxs:Receivables xmlns:maxs='http://www.microarea.it/Schema/2004/Smart/ERP/AP_AR/Receivables/Standard/Tcpos_Receivables.xsd' tbNamespace='Document.ERP.AP_AR.Documents.Receivables' xTechProfile='Tcpos_Receivables'>
                    <maxs:Data>
                        <maxs:AP_AR master='true'>
                            <maxs:CustSuppType>3211265</maxs:CustSuppType>
                            <maxs:CustSupp>{tbxSupplierCode.Text}</maxs:CustSupp>
                            <maxs:Payment>{tbxPaymentTerm.Text}</maxs:Payment>
                            <maxs:DocNo>{tbxDocumentDate.Text}</maxs:DocNo>
                            <maxs:DocumentDate>{tbxDocumentDate.Text}</maxs:DocumentDate>
                            <maxs:TotalAmount>{tbxTotalAmount.Text}</maxs:TotalAmount>
                            <maxs:TaxAmount>{tbxTaxAmount.Text}</maxs:TaxAmount>
                            <maxs:JournalEntryId>{tbxJournalEntryID.Text}</maxs:JournalEntryId>
                        </maxs:AP_AR>
                    </maxs:Data>
                </maxs:Receivables>");

            /*
            It is possible to use a custom set of installments instead of accepting the automatic calculation 
            */

            XNamespace maxs = xmlPayable.Name.NamespaceName;
            var data = xmlPayable.Descendants(maxs + "Data").FirstOrDefault();
            var detail = new XElement("Detail");
            data.Add(detail);

            int instNo = 1;
            foreach (var installment in installments)
            {
                XElement xmlInstallment = XElement.Parse($@"
                <maxs:DetailRow xmlns:maxs='http://www.microarea.it/Schema/2004/Smart/ERP/AP_AR/Receivables/Standard/Tcpos_Receivables.xsd'>
                    <maxs:InstallmentNo>{instNo++}</maxs:InstallmentNo>
                    <maxs:InstallmentType>{installment.Type}</maxs:InstallmentType>
                    <maxs:ClosingType>6946816</maxs:ClosingType>
                    <maxs:InstallmentDate>{installment.Date}</maxs:InstallmentDate>
                    <maxs:DebitCreditSign>4980736</maxs:DebitCreditSign>
                    <maxs:Amount>{installment.Amount.ToString(CultureInfo.InvariantCulture)}</maxs:Amount>
                </maxs:DetailRow>");

                detail.Add(xmlInstallment);
            }
            /**/

            using (MagoTBServices.TbServicesSoapClient aTbSvc = new MagoTBServices.TbServicesSoapClient())
            {
                aTbSvc.Endpoint.Address = new System.ServiceModel.EndpointAddress($"http://{server}/{instance}/TBServices/TBServices.asmx");

                string strResult;
                bool bSuccess = aTbSvc.SetData(authenticationToken, xmlPayable.ToString(), DateTime.Now, 0, true, out strResult);
                if (bSuccess)
                {
                    XElement xmlResult = XElement.Parse(strResult);

                    rtbxResults.Text = $"success \n{xmlResult}\n";
                }
                else
                {
                    rtbxResults.Text = $"!!!FAILED!!! \n[{strResult}]\n";
                }
            }

        }
    }
    public class Installment
    {
        public string Date = string.Empty;
        public double Amount = 0.0;
        public int Type = 0;
    }
}





