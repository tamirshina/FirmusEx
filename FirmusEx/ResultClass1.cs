using System;
using System.Collections.Generic;

namespace FirmusEx
{
    internal class ResultClass
    {
		//fields 	
		public int tableId
		{ get; set; }
		public string docTitle
		{ get; set; }
		public bool isStuffOnTable
		{ get; set; }
		public List<string> elmentsOnTable
		{ get; set; }

		public ResultClass(int tableId, string docTitle, bool isStuffOnTable, List<string> elmentsOnTable)
		{
			this.tableId = tableId;
			this.docTitle = docTitle;
			this.isStuffOnTable = isStuffOnTable;
			this.elmentsOnTable = elmentsOnTable;
		}

		private string createIntersrectingElementsString(List<string> elmentsOnTable)
		{
			string str = "";
			foreach (string str1 in elmentsOnTable)
			{
				str += str1 + Environment.NewLine;
			}
			return str;
		}

		public string instancePrint()
		{
			return "table Id - " + this.tableId + " ," + "docTitle - " + this.docTitle + " ," + "is stuff - " +
			this.isStuffOnTable.ToString() + " ," + "list of els -" + createIntersrectingElementsString(this.elmentsOnTable);
		}
    }
}