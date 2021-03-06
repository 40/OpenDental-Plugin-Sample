using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OpenDental;
using OpenDental.UI;
using OpenDentBusiness;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;

namespace PluginExample {
	public class FormRpStatementP {

		///<summary>Supply pd so that we know the paper size and margins.</summary>
		public static void CreateDocument(FormRpStatement sender,MigraDoc.DocumentObjectModel.Document doc,PrintDocument pd,Family fam,Patient pat,DataSet dataSet,Statement Stmt) {
			//doc= new MigraDoc.DocumentObjectModel.Document();//don't do this or the reference to the original doc will be lost.
			doc.DefaultPageSetup.PageWidth=Unit.FromInch((double)pd.DefaultPageSettings.PaperSize.Width/100);
			doc.DefaultPageSetup.PageHeight=Unit.FromInch((double)pd.DefaultPageSettings.PaperSize.Height/100);
			doc.DefaultPageSetup.TopMargin=Unit.FromInch((double)pd.DefaultPageSettings.Margins.Top/100);
			doc.DefaultPageSetup.LeftMargin=Unit.FromInch((double)pd.DefaultPageSettings.Margins.Left/100);
			doc.DefaultPageSetup.RightMargin=Unit.FromInch((double)pd.DefaultPageSettings.Margins.Right/100);
			doc.DefaultPageSetup.BottomMargin=Unit.FromInch((double)pd.DefaultPageSettings.Margins.Bottom/100);
			MigraDoc.DocumentObjectModel.Section section=doc.AddSection();//so that Swiss will have different footer for each patient.
			string text;
			MigraDoc.DocumentObjectModel.Font font;
			//GetPatGuar(PatNums[famIndex][0]);
			//Family fam=Patients.GetFamily(Stmt.PatNum);
			Patient PatGuar=fam.ListPats[0];//.Copy();
			//Patient pat=fam.GetPatient(Stmt.PatNum);
			DataTable tableMisc=dataSet.Tables["misc"];
			//HEADING------------------------------------------------------------------------------
			#region Heading
			Paragraph par=section.AddParagraph();
			ParagraphFormat parformat=new ParagraphFormat();
			parformat.Alignment=ParagraphAlignment.Center;
			par.Format=parformat;
			font=MigraDocHelper.CreateFont(14,true);
			text="This statement was generated from the plug-in";
			par.AddFormattedText(text,font);
			text=DateTime.Today.ToShortDateString();
			font=MigraDocHelper.CreateFont(10);
			par.AddLineBreak();
			par.AddFormattedText(text,font);
			text=Lan.g("FormRpStatement","Account Number")+" ";
			if(PrefC.GetBool(PrefName.StatementAccountsUseChartNumber)) {
				text+=PatGuar.ChartNumber;
			}
			else {
				text+=PatGuar.PatNum;
			}
			par.AddLineBreak();
			par.AddFormattedText(text,font);
			TextFrame frame;
			#endregion
			//Practice Address----------------------------------------------------------------------
			#region Practice Address
			if(PrefC.GetBool(PrefName.StatementShowReturnAddress)) {
				font=MigraDocHelper.CreateFont(10);
				frame=section.AddTextFrame();
				frame.RelativeVertical=RelativeVertical.Page;
				frame.RelativeHorizontal=RelativeHorizontal.Page;
				frame.MarginLeft=Unit.Zero;
				frame.MarginTop=Unit.Zero;
				frame.Top=TopPosition.Parse("0.5 in");
				frame.Left=LeftPosition.Parse("0.3 in");
				frame.Width=Unit.FromInch(3);
				if(!PrefC.GetBool(PrefName.EasyNoClinics) && Clinics.List.Length>0 //if using clinics
						&& Clinics.GetClinic(PatGuar.ClinicNum)!=null)//and this patient assigned to a clinic
					{
					Clinic clinic=Clinics.GetClinic(PatGuar.ClinicNum);
					par=frame.AddParagraph();
					par.Format.Font=font;
					par.AddText(clinic.Description);
					par.AddLineBreak();
					par.AddText(clinic.Address);
					par.AddLineBreak();
					if(clinic.Address2!="") {
						par.AddText(clinic.Address2);
						par.AddLineBreak();
					}
					if(CultureInfo.CurrentCulture.Name.EndsWith("CH")) {//CH is for switzerland. eg de-CH
						par.AddText(clinic.Zip+" "+clinic.City);
					}
					else {
						par.AddText(clinic.City+", "+clinic.State+" "+clinic.Zip);
					}
					par.AddLineBreak();
					text=clinic.Phone;
					if(text.Length==10) {
						text="("+text.Substring(0,3)+")"+text.Substring(3,3)+"-"+text.Substring(6);
					}
					par.AddText(text);
					par.AddLineBreak();
				}
				else {
					par=frame.AddParagraph();
					par.Format.Font=font;
					par.AddText(PrefC.GetString(PrefName.PracticeTitle));
					par.AddLineBreak();
					if(CultureInfo.CurrentCulture.Name=="en-AU") {//English (Australia)
						Provider defaultProv=Providers.GetProv(PrefC.GetLong(PrefName.PracticeDefaultProv));
						par.AddText("ABN: "+defaultProv.NationalProvID);
						par.AddLineBreak();
					}
					par.AddText(PrefC.GetString(PrefName.PracticeAddress));
					par.AddLineBreak();
					if(PrefC.GetString(PrefName.PracticeAddress2)!="") {
						par.AddText(PrefC.GetString(PrefName.PracticeAddress2));
						par.AddLineBreak();
					}
					if(CultureInfo.CurrentCulture.Name.EndsWith("CH")) {//CH is for switzerland. eg de-CH
						par.AddText(PrefC.GetString(PrefName.PracticeZip)+" "+PrefC.GetString(PrefName.PracticeCity));
					}
					else {
						par.AddText(PrefC.GetString(PrefName.PracticeCity)+", "+PrefC.GetString(PrefName.PracticeST)+" "+PrefC.GetString(PrefName.PracticeZip));
					}
					par.AddLineBreak();
					text=PrefC.GetString(PrefName.PracticePhone);
					if(text.Length==10) {
						text="("+text.Substring(0,3)+")"+text.Substring(3,3)+"-"+text.Substring(6);
					}
					par.AddText(text);
					par.AddLineBreak();
				}
			}
			#endregion
			//AMOUNT ENCLOSED------------------------------------------------------------------------------------------------------
			#region Amount Enclosed
			Table table;
			Column col;
			Row row;
			Cell cell;
			frame=MigraDocHelper.CreateContainer(section,450,110,330,29);
			if(!Stmt.HidePayment) {
				table=MigraDocHelper.DrawTable(frame,0,0,29);
				col=table.AddColumn(Unit.FromInch(1.1));
				col=table.AddColumn(Unit.FromInch(1.1));
				col=table.AddColumn(Unit.FromInch(1.1));
				row=table.AddRow();
				row.Format.Alignment=ParagraphAlignment.Center;
				row.Borders.Color=Colors.Black;
				row.Shading.Color=Colors.LightGray;
				row.TopPadding=Unit.FromInch(0);
				row.BottomPadding=Unit.FromInch(0);
				font=MigraDocHelper.CreateFont(8,true);
				cell=row.Cells[0];
				par=cell.AddParagraph();
				par.AddFormattedText(Lan.g("FormRpStatement","Amount Due"),font);
				cell=row.Cells[1];
				par=cell.AddParagraph();
				par.AddFormattedText(Lan.g("FormRpStatement","Date Due"),font);
				cell=row.Cells[2];
				par=cell.AddParagraph();
				par.AddFormattedText(Lan.g("FormRpStatement","Amount Enclosed"),font);
				row=table.AddRow();
				row.Format.Alignment=ParagraphAlignment.Center;
				row.Borders.Left.Color=Colors.Gray;
				row.Borders.Bottom.Color=Colors.Gray;
				row.Borders.Right.Color=Colors.Gray;
				font=MigraDocHelper.CreateFont(9);
				double balTotal=PatGuar.BalTotal;
				if(!PrefC.GetBool(PrefName.BalancesDontSubtractIns)) {//this is typical
					balTotal-=PatGuar.InsEst;
				}
				for(int m=0;m<tableMisc.Rows.Count;m++) {
					if(tableMisc.Rows[m]["descript"].ToString()=="payPlanDue") {
						balTotal+=PIn.Double(tableMisc.Rows[m]["value"].ToString());
						//payPlanDue;//PatGuar.PayPlanDue;
					}
				}
				text=balTotal.ToString("F");
				cell=row.Cells[0];
				par=cell.AddParagraph();
				par.AddFormattedText(text,font);
				if(PrefC.GetLong(PrefName.StatementsCalcDueDate)==-1) {
					text=Lan.g("FormRpStatement","Upon Receipt");
				}
				else {
					text=DateTime.Today.AddDays(PrefC.GetLong(PrefName.StatementsCalcDueDate)).ToShortDateString();
				}
				cell=row.Cells[1];
				par=cell.AddParagraph();
				par.AddFormattedText(text,font);
			}
			#endregion
			//Credit Card Info--------------------------------------------------------------------------------------------------------
			#region Credit Card Info
			if(!Stmt.HidePayment) {
				if(PrefC.GetBool(PrefName.StatementShowCreditCard)) {
					float yPos=60;
					font=MigraDocHelper.CreateFont(7,true);
					text=Lan.g("FormRpStatement","CREDIT CARD TYPE");
					MigraDocHelper.DrawString(frame,text,font,0,yPos);
					float rowHeight=26;
					System.Drawing.Font wfont=new System.Drawing.Font("Arial",7,FontStyle.Bold);
					System.Drawing.Image img=new Bitmap(500,30);
					Graphics g=Graphics.FromImage(img);//just to measure strings
					MigraDocHelper.DrawLine(frame,System.Drawing.Color.Black,g.MeasureString(text,wfont).Width,
						yPos+wfont.GetHeight(g),326,yPos+wfont.GetHeight(g));
					yPos+=rowHeight;
					text=Lan.g("FormRpStatement","#");
					MigraDocHelper.DrawString(frame,text,font,0,yPos);
					MigraDocHelper.DrawLine(frame,System.Drawing.Color.Black,g.MeasureString(text,wfont).Width,
						yPos+wfont.GetHeight(g),326,yPos+wfont.GetHeight(g));
					yPos+=rowHeight;
					text=Lan.g("FormRpStatement","3 DIGIT CSV");
					MigraDocHelper.DrawString(frame,text,font,0,yPos);
					MigraDocHelper.DrawLine(frame,System.Drawing.Color.Black,g.MeasureString(text,wfont).Width,
						yPos+wfont.GetHeight(g),326,yPos+wfont.GetHeight(g));
					yPos+=rowHeight;
					text=Lan.g("FormRpStatement","EXPIRES");
					MigraDocHelper.DrawString(frame,text,font,0,yPos);
					MigraDocHelper.DrawLine(frame,System.Drawing.Color.Black,g.MeasureString(text,wfont).Width,
						yPos+wfont.GetHeight(g),326,yPos+wfont.GetHeight(g));
					yPos+=rowHeight;
					text=Lan.g("FormRpStatement","AMOUNT APPROVED");
					MigraDocHelper.DrawString(frame,text,font,0,yPos);
					MigraDocHelper.DrawLine(frame,System.Drawing.Color.Black,g.MeasureString(text,wfont).Width,
						yPos+wfont.GetHeight(g),326,yPos+wfont.GetHeight(g));
					yPos+=rowHeight;
					text=Lan.g("FormRpStatement","NAME");
					MigraDocHelper.DrawString(frame,text,font,0,yPos);
					MigraDocHelper.DrawLine(frame,System.Drawing.Color.Black,g.MeasureString(text,wfont).Width,
						yPos+wfont.GetHeight(g),326,yPos+wfont.GetHeight(g));
					yPos+=rowHeight;
					text=Lan.g("FormRpStatement","SIGNATURE");
					MigraDocHelper.DrawString(frame,text,font,0,yPos);
					MigraDocHelper.DrawLine(frame,System.Drawing.Color.Black,g.MeasureString(text,wfont).Width,
						yPos+wfont.GetHeight(g),326,yPos+wfont.GetHeight(g));
					yPos-=rowHeight;
					text=Lan.g("FormRpStatement","(As it appears on card)");
					wfont=new System.Drawing.Font("Arial",5);
					font=MigraDocHelper.CreateFont(5);
					MigraDocHelper.DrawString(frame,text,font,625-g.MeasureString(text,wfont).Width/2+5,yPos+13);
					g.Dispose();
					img=null;
				}
			}
			#endregion
			//Patient's Billing Address---------------------------------------------------------------------------------------------
			#region Patient Billing Address
			font=MigraDocHelper.CreateFont(11);
			frame=MigraDocHelper.CreateContainer(section,62.5f+12.5f,225+1,300,200);
			par=frame.AddParagraph();
			par.Format.Font=font;
			if(Stmt.SinglePatient) {
				par.AddText(fam.GetNameInFamFL(Stmt.PatNum));
			}
			else {
				par.AddText(PatGuar.GetNameFLFormal());
			}
			par.AddLineBreak();
			par.AddText(PatGuar.Address);
			par.AddLineBreak();
			if(PatGuar.Address2!="") {
				par.AddText(PatGuar.Address2);
				par.AddLineBreak();
			}
			if(CultureInfo.CurrentCulture.Name.EndsWith("CH")) {//CH is for switzerland. eg de-CH
				par.AddText(PatGuar.Zip+" "+PatGuar.City);
			}
			else {
				par.AddText(PatGuar.City+", "+PatGuar.State+" "+PatGuar.Zip);
			}
			//perforated line------------------------------------------------------------------------------------------------------
			//yPos=350;//3.62 inches from top, 1/3 page down
			frame=MigraDocHelper.CreateContainer(section,0,350,850,30);
			if(!Stmt.HidePayment) {
				MigraDocHelper.DrawLine(frame,System.Drawing.Color.LightGray,0,0,850,0);
				text=Lan.g("FormRpStatement","PLEASE DETACH AND RETURN THE UPPER PORTION WITH YOUR PAYMENT");
				font=MigraDocHelper.CreateFont(6,true,System.Drawing.Color.Gray);
				par=frame.AddParagraph();
				par.Format.Alignment=ParagraphAlignment.Center;
				par.Format.Font=font;
				par.AddText(text);
			}
			#endregion
			//Australian Provider Legend
			#region Australian Provider Legend
			int legendOffset=0;
			if(CultureInfo.CurrentCulture.Name=="en-AU") {//English (Australia)
				Providers.RefreshCache();
				legendOffset=25+15*(1+ProviderC.List.Length);
				MigraDocHelper.InsertSpacer(section,legendOffset);
				frame=MigraDocHelper.CreateContainer(section,45,390,250,legendOffset);
				par=frame.AddParagraph();
				par.Format.Font=MigraDocHelper.CreateFont(8,true);
				par.AddLineBreak();
				par.AddText("PROVIDERS:");
				par=frame.AddParagraph();
				par.Format.Font=MigraDocHelper.CreateFont(8,false);
				for(int i=0;i<ProviderC.List.Length;i++) {//All non-hidden providers are added to the legend.
					Provider prov=ProviderC.List[i];
					string suffix="";
					if(prov.Suffix.Trim()!="") {
						suffix=", "+prov.Suffix.Trim();
					}
					par.AddText(prov.Abbr+" - "+prov.FName+" "+prov.LName+suffix+" - "+prov.MedicaidID);
					par.AddLineBreak();
				}
				par.AddLineBreak();
			}
			#endregion
			//Aging-----------------------------------------------------------------------------------
			#region Aging
			MigraDocHelper.InsertSpacer(section,275);
			frame=MigraDocHelper.CreateContainer(section,55,390+legendOffset,250,29);
			if(!Stmt.HidePayment) {
				table = MigraDocHelper.DrawTable(frame,0,0,29);
				col = table.AddColumn(Unit.FromInch(1.1));
				col = table.AddColumn(Unit.FromInch(1.1));
				col = table.AddColumn(Unit.FromInch(1.1));
				col = table.AddColumn(Unit.FromInch(1.1));
				row = table.AddRow();
				row.Format.Alignment = ParagraphAlignment.Center;
				row.Borders.Color = Colors.Black;
				row.Shading.Color = Colors.LightGray;
				row.TopPadding = Unit.FromInch(0);
				row.BottomPadding = Unit.FromInch(0);
				font = MigraDocHelper.CreateFont(8,true);
				cell = row.Cells[0];
				par = cell.AddParagraph();
				par.AddFormattedText(Lan.g("FormRpStatement","0-30"),font);
				cell = row.Cells[1];
				par = cell.AddParagraph();
				par.AddFormattedText(Lan.g("FormRpStatement","31-60"),font);
				cell = row.Cells[2];
				par = cell.AddParagraph();
				par.AddFormattedText(Lan.g("FormRpStatement","61-90"),font);
				cell = row.Cells[3];
				par = cell.AddParagraph();
				par.AddFormattedText(Lan.g("FormRpStatement","over 90"),font);
				row = table.AddRow();
				row.Format.Alignment = ParagraphAlignment.Center;
				row.Borders.Left.Color = Colors.Gray;
				row.Borders.Bottom.Color = Colors.Gray;
				row.Borders.Right.Color = Colors.Gray;
				font = MigraDocHelper.CreateFont(9);
				text= PatGuar.Bal_0_30.ToString("F");
				cell = row.Cells[0];
				par = cell.AddParagraph();
				par.AddFormattedText(text,font);
				text = PatGuar.Bal_31_60.ToString("F");
				cell = row.Cells[1];
				par = cell.AddParagraph();
				par.AddFormattedText(text,font);
				text = PatGuar.Bal_61_90.ToString("F");
				cell = row.Cells[2];
				par = cell.AddParagraph();
				par.AddFormattedText(text,font);
				text = PatGuar.BalOver90.ToString("F");
				cell = row.Cells[3];
				par = cell.AddParagraph();
				par.AddFormattedText(text,font);
			}
			#endregion
			//Floating Balance, Ins info-------------------------------------------------------------------
			#region FloatingBalance
			frame=MigraDocHelper.CreateContainer(section,460,380+legendOffset,250,200);
			//table=MigraDocHelper.DrawTable(frame,0,0,90);
			par = frame.AddParagraph();
			parformat = new ParagraphFormat();
			parformat.Alignment = ParagraphAlignment.Right;
			par.Format = parformat;
			font = MigraDocHelper.CreateFont(10,false);
			MigraDoc.DocumentObjectModel.Font fontBold=MigraDocHelper.CreateFont(10,true);
			if(PrefC.GetBool(PrefName.BalancesDontSubtractIns)) {
				text = Lan.g("FormRpStatement","Balance:");
				par.AddFormattedText(text,fontBold);
				//par.AddLineBreak();
				//text = Lan.g(this, "Ins Pending:");
				//par.AddFormattedText(text, font);
				//par.AddLineBreak();
				//text = Lan.g(this, "After Ins:");
				//par.AddFormattedText(text, font);
				//par.AddLineBreak();
			}
			else {//this is more common
				if(PrefC.GetBool(PrefName.FuchsOptionsOn)) {
					text = Lan.g("FormRpStatement","Balance:");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = Lan.g("FormRpStatement","-Ins Estimate:");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = Lan.g("FormRpStatement","=Owed Now:");
					par.AddFormattedText(text,fontBold);
					par.AddLineBreak();
				}
				else {
					text = Lan.g("FormRpStatement","Total:");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = Lan.g("FormRpStatement","-Ins Estimate:");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = Lan.g("FormRpStatement","=Balance:");
					par.AddFormattedText(text,fontBold);
					par.AddLineBreak();
				}
			}
			frame=MigraDocHelper.CreateContainer(section,730,380+legendOffset,100,200);
			//table=MigraDocHelper.DrawTable(frame,0,0,90);
			par = frame.AddParagraph();
			parformat = new ParagraphFormat();
			parformat.Alignment = ParagraphAlignment.Left;
			par.Format = parformat;
			font = MigraDocHelper.CreateFont(10,false);
			//numbers:
			if(PrefC.GetBool(PrefName.BalancesDontSubtractIns)) {
				text = PatGuar.BalTotal.ToString("c");
				par.AddFormattedText(text,fontBold);
				//par.AddLineBreak();
				//text = PatGuar.InsEst.ToString("c");
				//par.AddFormattedText(text, font);
				//par.AddLineBreak();
				//text = (PatGuar.BalTotal - PatGuar.InsEst).ToString("c");
				//par.AddFormattedText(text, font);
				//par.AddLineBreak();
			}
			else {//more common
				if(Stmt.SinglePatient) {
					double patInsEst=0;
					for(int m=0;m<tableMisc.Rows.Count;m++) {
						if(tableMisc.Rows[m]["descript"].ToString()=="patInsEst") {
							patInsEst=PIn.Double(tableMisc.Rows[m]["value"].ToString());
						}
					}
					double patBal=pat.EstBalance-patInsEst;
					text = pat.EstBalance.ToString("c");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = patInsEst.ToString("c");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = patBal.ToString("c");
					par.AddFormattedText(text,fontBold);
				}
				else {
					text = PatGuar.BalTotal.ToString("c");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = PatGuar.InsEst.ToString("c");
					par.AddFormattedText(text,font);
					par.AddLineBreak();
					text = (PatGuar.BalTotal - PatGuar.InsEst).ToString("c");
					par.AddFormattedText(text,fontBold);
					par.AddLineBreak();
				}
			}
			MigraDocHelper.InsertSpacer(section,80);
			#endregion FloatingBalance
			//Bold note-------------------------------------------------------------------------------
			#region Bold note
			if(Stmt.NoteBold!="") {
				MigraDocHelper.InsertSpacer(section,7);
				font=MigraDocHelper.CreateFont(10,true,System.Drawing.Color.DarkRed);
				par=section.AddParagraph();
				par.Format.Font=font;
				par.AddText(Stmt.NoteBold);
				MigraDocHelper.InsertSpacer(section,8);
			}
			#endregion Bold note
			//Payment plan grid definition---------------------------------------------------------------------------------
			#region PayPlan grid definition
			ODGridColumn gcol;
			ODGridRow grow;
			ODGrid gridPP = new ODGrid();
			//this.Controls.Add(gridPP);
			gridPP.BeginUpdate();
			gridPP.Columns.Clear();
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Date"),73);
			gridPP.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Description"),270);
			gridPP.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Charges"),60,HorizontalAlignment.Right);
			gridPP.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Credits"),60,HorizontalAlignment.Right);
			gridPP.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Balance"),60,HorizontalAlignment.Right);
			gridPP.Columns.Add(gcol);
			gridPP.Width=gridPP.WidthAllColumns+20;
			gridPP.EndUpdate();
			#endregion PayPlan grid definition
			//Payment plan grid.  There will be only one, if any-----------------------------------------------------------------
			#region PayPlan grid
			DataTable tablePP=dataSet.Tables["payplan"];
			ODGridCell gcell;
			if(tablePP.Rows.Count>0) {
				//MigraDocHelper.InsertSpacer(section,5);
				par=section.AddParagraph();
				par.Format.Font=MigraDocHelper.CreateFont(10,true);
				par.Format.Alignment=ParagraphAlignment.Center;
				//par.Format.SpaceBefore=Unit.FromInch(.05);
				//par.Format.SpaceAfter=Unit.FromInch(.05);
				par.AddText(Lan.g("FormRpStatement","Payment Plans"));
				MigraDocHelper.InsertSpacer(section,2);
				gridPP.BeginUpdate();
				gridPP.Rows.Clear();
				for(int p=0;p<tablePP.Rows.Count;p++) {
					grow=new ODGridRow();
					grow.Cells.Add(tablePP.Rows[p]["date"].ToString());
					grow.Cells.Add(tablePP.Rows[p]["description"].ToString());
					grow.Cells.Add(tablePP.Rows[p]["charges"].ToString());
					grow.Cells.Add(tablePP.Rows[p]["credits"].ToString());
					gcell=new ODGridCell(tablePP.Rows[p]["balance"].ToString());
					if(p==tablePP.Rows.Count-1) {
						gcell.Bold=YN.Yes;
					}
					else if(tablePP.Rows[p+1]["balance"].ToString()=="") {//if next row balance is blank.
						gcell.Bold=YN.Yes;
					}
					grow.Cells.Add(gcell);
					gridPP.Rows.Add(grow);
				}
				gridPP.EndUpdate();
				MigraDocHelper.DrawGrid(section,gridPP);
				MigraDocHelper.InsertSpacer(section,2);
				par=section.AddParagraph();
				par.Format.Font=MigraDocHelper.CreateFont(10,true);
				par.Format.Alignment=ParagraphAlignment.Right;
				par.Format.RightIndent=Unit.FromInch(0.25);
				double payPlanDue=0;
				for(int m=0;m<tableMisc.Rows.Count;m++) {
					if(tableMisc.Rows[m]["descript"].ToString()=="payPlanDue") {
						payPlanDue=PIn.Double(tableMisc.Rows[m]["value"].ToString());
					}
				}
				par.AddText(Lan.g("FormRpStatement","Payment Plan Amount Due: ")+payPlanDue.ToString("c"));//PatGuar.PayPlanDue.ToString("c"));
				MigraDocHelper.InsertSpacer(section,10);
			}
			#endregion PayPlan grid
			//Body Table definition--------------------------------------------------------------------------------------------------------
			#region Body Table definition
			ODGrid gridPat = new ODGrid();
			//this.Controls.Add(gridPat);
			gridPat.BeginUpdate();
			gridPat.Columns.Clear();
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Date"),73);
			gridPat.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Patient"),100);
			gridPat.Columns.Add(gcol);
			//prov
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Code"),45);
			gridPat.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Tooth"),42);
			gridPat.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Description"),270);
			gridPat.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Charges"),60,HorizontalAlignment.Right);
			gridPat.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Credits"),60,HorizontalAlignment.Right);
			gridPat.Columns.Add(gcol);
			gcol=new ODGridColumn(Lan.g("FormRpStatement","Balance"),60,HorizontalAlignment.Right);
			gridPat.Columns.Add(gcol);
			gridPat.Width=gridPat.WidthAllColumns+20;
			gridPat.EndUpdate();
			#endregion Body Table definition
			//Loop through each table.  Could be one intermingled, or one for each patient-----------------------------------------
			DataTable tableAccount;
			string tablename;
			long patnum;
			for(int i=0;i<dataSet.Tables.Count;i++) {
				tableAccount=dataSet.Tables[i];
				tablename=tableAccount.TableName;
				if(!tablename.StartsWith("account")) {
					continue;
				}
				par=section.AddParagraph();
				par.Format.Font=MigraDocHelper.CreateFont(10,true);
				par.Format.SpaceBefore=Unit.FromInch(.05);
				par.Format.SpaceAfter=Unit.FromInch(.05);
				patnum=0;
				if(tablename!="account") {//account123 etc.
					patnum=PIn.Long(tablename.Substring(7));
				}
				if(patnum!=0) {
					par.AddText(fam.GetNameInFamFLnoPref(patnum));
				}
				//if(FamilyStatementDataList[famIndex].PatAboutList[i].ApptDescript!=""){
				//	par=section.AddParagraph();
				//	par.Format.Font=MigraDocHelper.CreateFont(9);//same as body font
				//	par.AddText(FamilyStatementDataList[famIndex].PatAboutList[i].ApptDescript);
				//}
				gridPat.BeginUpdate();
				gridPat.Rows.Clear();
				//lineData=FamilyStatementDataList[famIndex].PatDataList[i].PatData;
				for(int p=0;p<tableAccount.Rows.Count;p++) {
					grow=new ODGridRow();
					grow.Cells.Add(tableAccount.Rows[p]["date"].ToString());
					grow.Cells.Add(tableAccount.Rows[p]["patient"].ToString());
					grow.Cells.Add(tableAccount.Rows[p]["ProcCode"].ToString());
					grow.Cells.Add(tableAccount.Rows[p]["tth"].ToString());
					if(CultureInfo.CurrentCulture.Name=="en-AU") {//English (Australia)
						if(tableAccount.Rows[p]["prov"].ToString().Trim()!="") {
							grow.Cells.Add(tableAccount.Rows[p]["prov"].ToString()+" - "+tableAccount.Rows[p]["description"].ToString());
						}
						else {//No provider on this account row item, so don't put the extra leading characters.
							grow.Cells.Add(tableAccount.Rows[p]["description"].ToString());
						}
					}
					else {//Assume English (United States)
						grow.Cells.Add(tableAccount.Rows[p]["description"].ToString());
					}
					grow.Cells.Add(tableAccount.Rows[p]["charges"].ToString());
					grow.Cells.Add(tableAccount.Rows[p]["credits"].ToString());
					grow.Cells.Add(tableAccount.Rows[p]["balance"].ToString());
					gridPat.Rows.Add(grow);
				}
				gridPat.EndUpdate();
				MigraDocHelper.DrawGrid(section,gridPat);
				//Total
				frame=MigraDocHelper.CreateContainer(section);
				font=MigraDocHelper.CreateFont(9,true);
				float totalPos=((float)(doc.DefaultPageSetup.PageWidth.Inch//-doc.DefaultPageSetup.LeftMargin.Inch
					//-doc.DefaultPageSetup.RightMargin.Inch)
					)*100f)/2f+(float)gridPat.WidthAllColumns/2f+7;
				RectangleF rectF=new RectangleF(0,0,totalPos,16);
				if(patnum!=0) {
					MigraDocHelper.DrawString(frame," ",
						//I decided this was unnecessary:
						//dataSet.Tables["patient"].Rows[fam.GetIndex(patnum)]["balance"].ToString(),
						font,rectF,ParagraphAlignment.Right);
					//MigraDocHelper.DrawString(frame,FamilyStatementDataList[famIndex].PatAboutList[i].Balance.ToString("F"),font,rectF,
					//	ParagraphAlignment.Right);
				}
			}
			gridPat.Dispose();
			//Future appointments---------------------------------------------------------------------------------------------
			#region Future appointments
			font=MigraDocHelper.CreateFont(9);
			DataTable tableAppt=dataSet.Tables["appts"];
			if(tableAppt.Rows.Count>0) {
				par=section.AddParagraph();
				par.Format.Font=font;
				par.AddText(Lan.g("FormRpStatement","Scheduled Appointments:"));
			}
			for(int i=0;i<tableAppt.Rows.Count;i++) {
				par.AddLineBreak();
				par.AddText(tableAppt.Rows[i]["descript"].ToString());
			}
			if(tableAppt.Rows.Count>0) {
				MigraDocHelper.InsertSpacer(section,10);
			}
			#endregion Future appointments
			//Note------------------------------------------------------------------------------------------------------------
			font=MigraDocHelper.CreateFont(9);
			par=section.AddParagraph();
			par.Format.Font=font;
			par.AddText(Stmt.Note);
			//bold note
			if(Stmt.NoteBold!="") {
				MigraDocHelper.InsertSpacer(section,10);
				font=MigraDocHelper.CreateFont(10,true,System.Drawing.Color.DarkRed);
				par=section.AddParagraph();
				par.Format.Font=font;
				par.AddText(Stmt.NoteBold);
			}
			//return doc;
		}



	}
}