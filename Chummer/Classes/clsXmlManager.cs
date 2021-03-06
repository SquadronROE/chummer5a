/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Chummer
{
    // ReSharper disable InconsistentNaming
    public static class XmlManager
    {
        /// <summary>
        /// Used to cache XML files so that they do not need to be loaded and translated each time an object wants the file.
        /// </summary>
        private sealed class XmlReference
        {
            /// <summary>
            /// Date/Time stamp on the XML file.
            /// </summary>
            public DateTime FileDate { get; set; }

            /// <summary>
            /// Name of the XML file.
            /// </summary>
            public string FileName { get; set; } = string.Empty;

            /// <summary>
            /// Language of the XML file.
            /// </summary>
            public string Language { get; set; } = GlobalOptions.DefaultLanguage;

            /// <summary>
            /// Whether or not the XML file has been successfully checked for duplicate guids.
            /// </summary>
            public bool DuplicatesChecked { get; set; }

            /// <summary>
            /// XmlDocument that is created by merging the base data file and data translation file. Does not include custom content since this must be loaded each time.
            /// </summary>
            public XmlDocument XmlContent { get; set; } = new XmlDocument();
        }

        private static readonly HashSet<XmlReference> s_LstXmlDocuments = new HashSet<XmlReference>();
        private static readonly object s_LstXmlDocumentsLock = new object();
        private static readonly List<string> s_LstDataDirectories = new List<string>();

        #region Constructor
        static XmlManager()
        {
            s_LstDataDirectories.Add(Path.Combine(Application.StartupPath, "data"));
            foreach (CustomDataDirectoryInfo objCustomDataDirectory in GlobalOptions.CustomDataDirectoryInfo.Where(x => x.Enabled))
            {
                s_LstDataDirectories.Add(objCustomDataDirectory.Path);
            }
        }

        #endregion

        #region Methods
        /// <summary>
        /// Load the selected XML file and its associated custom file.
        /// </summary>
        /// <param name="strFileName">Name of the XML file to load.</param>
        /// <param name="blnLoadFile">Whether to force reloading content even if the file already exists.</param>
        public static XmlDocument Load(string strFileName, string strLanguage = "", bool blnLoadFile = false)
        {
            bool blnFileFound = false;
            string strPath = string.Empty;
            foreach (string strDirectory in s_LstDataDirectories)
            {
                strPath = Path.Combine(strDirectory, strFileName);
                if (File.Exists(strPath))
                {
                    blnFileFound = true;
                    break;
                }
            }
            if (!blnFileFound)
            {
                Utils.BreakIfDebug();
                return null;
            }

            DateTime datDate = File.GetLastWriteTime(strPath);
            if (string.IsNullOrEmpty(strLanguage))
                strLanguage = GlobalOptions.Language;

            // Look to see if this XmlDocument is already loaded.
            XmlReference objReference = null;
            lock (s_LstXmlDocumentsLock)
            {
                objReference = s_LstXmlDocuments.FirstOrDefault(x => x.FileName == strFileName);
                if (objReference == null || blnLoadFile)
                {
                    // The file was not found in the reference list, so it must be loaded.
                    objReference = new XmlReference();
                    blnLoadFile = true;
                    s_LstXmlDocuments.Add(objReference);
                }
                // The file was found in the List, so check the last write time and language.
                else if (datDate != objReference.FileDate || strLanguage != objReference.Language)
                {
                    // The last write time and/or language does not match, so it must be reloaded.
                    blnLoadFile = true;
                }
            }

            // Create a new document that everything will be merged into.
            XmlDocument objDoc;
            XmlDocument objXmlFile = new XmlDocument();

            if (blnLoadFile)
            {
                objDoc = new XmlDocument();
                // write the root chummer node.
                XmlNode objCont = objDoc.CreateElement("chummer");
                objDoc.AppendChild(objCont);
                XmlElement objDocElement = objDoc.DocumentElement;
                // Load the base file and retrieve all of the child nodes.
                objXmlFile.Load(strPath);
                XmlNodeList xmlNodeList = objXmlFile.SelectNodes("/chummer/*");
                if (xmlNodeList != null)
                    foreach (XmlNode objNode in xmlNodeList)
                    {
                        // Append the entire child node to the new document.
                        objDocElement.AppendChild(objDoc.ImportNode(objNode, true));
                    }

                // Load any override data files the user might have. Do not attempt this if we're loading the Improvements file.
                if (strFileName != "improvements.xml")
                {
                    foreach (string strLoopPath in s_LstDataDirectories)
                    {
                        foreach (string strFile in Directory.GetFiles(strLoopPath, "override*_" + strFileName))
                        {
                            objXmlFile.Load(strFile);
                            foreach (XmlNode objNode in objXmlFile.SelectNodes("/chummer/*"))
                            {
                                foreach (XmlNode objType in objNode.ChildNodes)
                                {
                                    string strFilter = string.Empty;
                                    if (objType["id"] != null)
                                        strFilter = "id = \"" + objType["id"].InnerText.Replace("&amp;", "&") + "\"";
                                    else if (objType["name"] != null)
                                        strFilter = "name = \"" + objType["name"].InnerText.Replace("&amp;", "&") + "\"";
                                    // Child Nodes marked with "isidnode" serve as additional identifier nodes, in case something needs modifying that uses neither a name nor an ID.
                                    XmlNodeList objAmendingNodeExtraIds = objType.SelectNodes("child::*[@isidnode = \"yes\"]");
                                    foreach (XmlNode objExtraId in objAmendingNodeExtraIds)
                                    {
                                        if (!string.IsNullOrEmpty(strFilter))
                                            strFilter += " and ";
                                        strFilter += objExtraId.Name + " = \"" + objExtraId.InnerText.Replace("&amp;", "&") + "\"";
                                    }
                                    if (!string.IsNullOrEmpty(strFilter))
                                    {
                                        XmlNode objItem = objDoc.SelectSingleNode("/chummer/" + objNode.Name + "/" + objType.Name + "[" + strFilter + "]");
                                        if (objItem != null)
                                            objItem.InnerXml = objType.InnerXml;
                                    }
                                }
                            }
                        }

                        // Load any custom data files the user might have. Do not attempt this if we're loading the Improvements file.
                        foreach (string strFile in Directory.GetFiles(strLoopPath, "custom*_" + strFileName))
                        {
                            objXmlFile.Load(strFile);
                            foreach (XmlNode objNode in objXmlFile.SelectNodes("/chummer/*"))
                            {
                                // Look for any items with a duplicate name and pluck them from the node so we don't end up with multiple items with the same name.
                                List<XmlNode> lstDelete = new List<XmlNode>();
                                foreach (XmlNode objChild in objNode.ChildNodes)
                                {
                                    XmlNode objParentNode = objChild.ParentNode;
                                    if (objParentNode != null)
                                    {
                                        string strFilter = string.Empty;
                                        if (objChild["id"] != null)
                                            strFilter = "id = \"" + objChild["id"].InnerText.Replace("&amp;", "&") + "\"";
                                        if (objChild["name"] != null)
                                        {
                                            if (!string.IsNullOrEmpty(strFilter))
                                                strFilter += " and ";
                                            strFilter += "name = \"" + objChild["name"].InnerText.Replace("&amp;", "&") + "\"";
                                        }
                                        // Only do this if the child has the name or id field since this is what we must match on.
                                        if (!string.IsNullOrEmpty(strFilter))
                                        {
                                            XmlNode objItem = objDoc.SelectSingleNode("/chummer/" + objParentNode.Name + "/" + objChild.Name + "[" + strFilter + "]");
                                            if (objItem != null)
                                                lstDelete.Add(objChild);
                                        }
                                    }
                                }
                                // Remove the offending items from the node we're about to merge in.
                                foreach (XmlNode objRemoveNode in lstDelete)
                                {
                                    objNode.RemoveChild(objRemoveNode);
                                }

                                // Append the entire child node to the new document.
                                objDocElement.AppendChild(objDoc.ImportNode(objNode, true));
                            }
                        }

                        // Load any amending data we might have, i.e. rules that only amend items instead of replacing them. Do not attempt this if we're loading the Improvements file.
                        foreach (string strFile in Directory.GetFiles(strLoopPath, "amend*_" + strFileName))
                        {
                            objXmlFile.Load(strFile);
                            foreach (XmlNode objNode in objXmlFile.SelectNodes("/chummer/*"))
                            {
                                AmendNodeChildern(objDoc, objNode, "/chummer");
                            }
                        }
                    }
                }

                // Load the translation file for the current base data file if the selected language is not en-us.
                if (strLanguage != GlobalOptions.DefaultLanguage)
                {
                    // Everything is stored in the selected language file to make translations easier, keep all of the language-specific information together, and not require users to download 27 individual files.
                    // The structure is similar to the base data file, but the root node is instead a child /chummer node with a file attribute to indicate the XML file it translates.
                    XmlDocument objDataDoc = LanguageManager.GetDataDocument(strLanguage);
                    if (objDataDoc != null)
                    {
                        foreach (XmlNode objNode in objDataDoc?.SelectNodes("/chummer/chummer[@file = \"" + strFileName + "\"]"))
                        {
                            foreach (XmlNode objType in objNode.ChildNodes)
                            {
                                foreach (XmlNode objChild in objType.ChildNodes)
                                {
                                    XmlNode xmlNameNode = objChild["name"];
                                    if (xmlNameNode != null)
                                    {
                                        // If this is a translatable item, find the proper node and add/update this information.
                                        XmlNode objItem =
                                            objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[name = \"" +
                                                                    xmlNameNode.InnerXml.Replace("&amp;", "&") + "\"]");
                                        if (objItem != null)
                                        {
                                            string strAppendInnerXml = string.Empty;
                                            XmlNode xmlLoopNode = objChild["translate"];
                                            if (xmlLoopNode != null)
                                            {
                                                strAppendInnerXml += "<translate>" + xmlLoopNode.InnerXml + "</translate>";
                                            }
                                            xmlLoopNode = objChild["altpage"];
                                            if (xmlLoopNode != null)
                                            {
                                                strAppendInnerXml += "<altpage>" + xmlLoopNode.InnerXml + "</altpage>";
                                            }
                                            xmlLoopNode = objChild["code"];
                                            if (xmlLoopNode != null)
                                            {
                                                strAppendInnerXml += "<altcode>" + xmlLoopNode.InnerXml + "</altcode>";
                                            }
                                            xmlLoopNode = objChild["altadvantage"];
                                            if (xmlLoopNode != null)
                                            {
                                                strAppendInnerXml += "<altadvantage>" + xmlLoopNode.InnerXml + "</altadvantage>";
                                            }
                                            xmlLoopNode = objChild["altdisadvantage"];
                                            if (xmlLoopNode != null)
                                            {
                                                strAppendInnerXml += "<altdisadvantage>" + xmlLoopNode.InnerXml + "</altdisadvantage>";
                                            }
                                            if (!string.IsNullOrEmpty(strAppendInnerXml))
                                                objItem.InnerXml += strAppendInnerXml;
                                            xmlLoopNode = objChild.Attributes?["translate"];
                                            if (xmlLoopNode != null)
                                            {
                                                // Handle Category name translations.
                                                (objItem as XmlElement)?.SetAttribute("translate", xmlLoopNode.InnerXml);
                                            }

                                            // Check for Skill Specialization information.
                                            switch (strFileName)
                                            {
                                                case "skills.xml":
                                                    if (objChild["specs"] != null)
                                                    {
                                                        foreach (XmlNode objSpec in objChild.SelectNodes("specs/spec"))
                                                        {
                                                            xmlLoopNode = objSpec.Attributes?["translate"];
                                                            if (xmlLoopNode != null)
                                                            {
                                                                XmlElement objSpecItem = objItem.SelectSingleNode("specs/spec[. = \"" + objSpec.InnerXml + "\"]") as XmlElement;
                                                                objSpecItem?.SetAttribute("translate", xmlLoopNode.InnerXml);
                                                            }
                                                        }
                                                    }
                                                    break;
                                                case "metatypes.xml":
                                                    if (objChild["metavariants"] != null)
                                                    {
                                                        foreach (XmlNode objMetavariant in objChild.SelectNodes("metavariants/metavariant"))
                                                        {
                                                            if (objMetavariant["name"] != null && objChild["name"] != null)
                                                            {
                                                                XmlNode objMetavariantItem =
                                                                    objDoc.SelectSingleNode(
                                                                        "/chummer/metatypes/metatype[name = \"" +
                                                                        objChild["name"].InnerXml +
                                                                        "\"]/metavariants/metavariant[name = \"" +
                                                                        objMetavariant["name"].InnerXml + "\"]");
                                                                if (objMetavariantItem != null)
                                                                {
                                                                    strAppendInnerXml = string.Empty;
                                                                    xmlLoopNode = objMetavariant["translate"];
                                                                    if (xmlLoopNode != null)
                                                                    {
                                                                        strAppendInnerXml += "<translate>" +
                                                                                                       xmlLoopNode.InnerXml +
                                                                                                       "</translate>";
                                                                    }
                                                                    xmlLoopNode = objMetavariant["altpage"];
                                                                    if (xmlLoopNode != null)
                                                                    {
                                                                        strAppendInnerXml += "<altpage>" +
                                                                                                       xmlLoopNode.InnerXml +
                                                                                                       "</altpage>";
                                                                    }
                                                                    if (!string.IsNullOrEmpty(strAppendInnerXml))
                                                                        objMetavariantItem.InnerXml += strAppendInnerXml;
                                                                }
                                                            }
                                                        }
                                                    }
                                                    break;
                                                case "mentors.xml":
                                                case "paragons.xml":
                                                    if (objChild["choices"] != null)
                                                    {
                                                        foreach (XmlNode objChoice in objChild.SelectNodes("choices/choice"))
                                                        {
                                                            if (objChoice["name"] != null && objChoice["translate"] != null)
                                                            {
                                                                XmlNode objChoiceItem = objItem.SelectSingleNode("choices/choice[name = \"" + objChoice["name"].InnerXml + "\"]");
                                                                if (objChoiceItem != null)
                                                                    objChoiceItem.InnerXml += "<translate>" + objChoice["translate"].InnerXml + "</translate>";
                                                            }
                                                        }
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else if (objChild.Attributes?["translate"] != null)
                                    {
                                        // Handle Category name translations.
                                        XmlElement objItem = objDoc.SelectSingleNode("/chummer/" + objType.Name + "/" + objChild.Name + "[. = \"" + objChild.InnerXml.Replace("&amp;", "&") + "\"]") as XmlElement;
                                        // Expected result is null if not found.
                                        objItem?.SetAttribute("translate", objChild.Attributes["translate"].InnerXml);
                                    }
                                }
                            }
                        }
                    }
                }

                // Cache the merged document and its relevant information.
                objReference.FileDate = datDate;
                objReference.FileName = strFileName;
                objReference.Language = strLanguage;
                if (GlobalOptions.LiveCustomData)
                    objReference.XmlContent = objDoc.Clone() as XmlDocument;
                else
                    objReference.XmlContent = objDoc;
            }
            else
            {
                // A new XmlDocument is created by loading the a copy of the cached one so that we don't stuff custom content into the cached copy
                // (which we don't want and also results in multiple copies of each custom item).
                // Pull the document from cache.
                if (GlobalOptions.LiveCustomData)
                    objDoc = objReference.XmlContent.Clone() as XmlDocument;
                else
                    objDoc = objReference.XmlContent;
            }

            // Load any custom data files the user might have. Do not attempt this if we're loading the Improvements file.
            bool blnHasLiveCustomData = false;
            if (GlobalOptions.LiveCustomData && objDoc != null && strFileName != "improvements.xml")
            {
                XmlElement objDocElement = objDoc.DocumentElement;
                strPath = Path.Combine(Application.StartupPath, "livecustomdata");
                if (Directory.Exists(strPath))
                {
                    foreach (string strFile in Directory.GetFiles(strPath, "custom*_" + strFileName, SearchOption.AllDirectories))
                    {
                        objXmlFile.Load(strFile);
                        foreach (XmlNode objNode in objXmlFile.SelectNodes("/chummer/*"))
                        {
                            blnHasLiveCustomData = true;
                            // Look for any items with a duplicate name and pluck them from the node so we don't end up with multiple items with the same name.
                            List<XmlNode> lstDelete = new List<XmlNode>();
                            foreach (XmlNode objChild in objNode.ChildNodes)
                            {
                                XmlNode objParentNode = objChild.ParentNode;
                                if (objParentNode != null)
                                {
                                    string strFilter = string.Empty;
                                    if (objChild["id"] != null)
                                        strFilter = "id = \"" + objChild["id"].InnerText.Replace("&amp;", "&") + "\"";
                                    if (objChild["name"] != null)
                                    {
                                        if (!string.IsNullOrEmpty(strFilter))
                                            strFilter += " and ";
                                        strFilter += "name = \"" + objChild["name"].InnerText.Replace("&amp;", "&") + "\"";
                                    }
                                    // Only do this if the child has the name or id field since this is what we must match on.
                                    if (!string.IsNullOrEmpty(strFilter))
                                    {
                                        XmlNode objItem = objDoc.SelectSingleNode("/chummer/" + objParentNode.Name + "/" + objChild.Name + "[" + strFilter + "]");
                                        if (objItem != null)
                                            lstDelete.Add(objChild);
                                    }
                                }
                            }
                            // Remove the offending items from the node we're about to merge in.
                            foreach (XmlNode objRemoveNode in lstDelete)
                            {
                                objNode.RemoveChild(objRemoveNode);
                            }

                            // Append the entire child node to the new document.
                            objDocElement.AppendChild(objDoc.ImportNode(objNode, true));
                        }
                    }
                }
            }

            //Check for non-unique guids and non-guid formatted ids in the loaded XML file. Ignore improvements.xml since the ids are used in a different way.
            if (strFileName == "improvements.xml" || (objReference.DuplicatesChecked && !blnHasLiveCustomData)) return objDoc;
            {
                foreach (XmlNode objNode in objDoc.SelectNodes("/chummer/*"))
                {
                    //Ignore the version node, if present. 
                    if (objNode.Name == "version" || !objNode.HasChildNodes) continue;
                    //Parse the node into an XDocument for LINQ parsing. 
                    XDocument y = XDocument.Parse(objNode.OuterXml);
                    string strNode = (from XmlNode o in objNode.ChildNodes where o.NodeType != XmlNodeType.Comment select o.Name).FirstOrDefault();

                    //Grab the first XML node that isn't a comment. 
                    if (strNode == null) continue;
                    HashSet<string> lstDuplicateIDs = new HashSet<string>();
                    List<string> lstItemsWithMalformedIDs = new List<string>();
                    // Not a dictionary specifically so duplicates can be caught. Item1 is ID, Item2 is the item's name.
                    List<Tuple<string, string>> lstItemsWithIDs = new List<Tuple<string, string>>();
                    foreach (XElement objLoopNode in y.Descendants(strNode))
                    {
                        string strId = (string)objLoopNode.Element("id") ?? string.Empty;
                        if (!string.IsNullOrEmpty(strId))
                        {
                            string strItemName = (string)objLoopNode.Element("name") ?? (string)objLoopNode.Element("category") ?? strId;
                            if (!lstDuplicateIDs.Contains(strId) && lstItemsWithIDs.Any(x => x.Item1 == strId))
                            {
                                lstDuplicateIDs.Add(strId);
                                if (strItemName == strId)
                                    strItemName = string.Empty;
                            }
                            if (!strId.IsGuid())
                                lstItemsWithMalformedIDs.Add(strItemName);
                            lstItemsWithIDs.Add(new Tuple<string, string>(strId, strItemName));
                        }
                    }

                    if (lstDuplicateIDs.Count > 0)
                    {
                        string strDuplicatesNames = string.Join("\n", lstItemsWithIDs.Where(x => lstDuplicateIDs.Contains(x.Item1) && !string.IsNullOrEmpty(x.Item2)).Select(x => x.Item2));
                        MessageBox.Show(
                            LanguageManager.GetString("Message_DuplicateGuidWarning", strLanguage)
                                .Replace("{0}", lstDuplicateIDs.Count.ToString())
                                .Replace("{1}", strFileName)
                                .Replace("{2}", strDuplicatesNames));
                    }

                    if (lstItemsWithMalformedIDs.Count > 0)
                    {
                        string strMalformedIdNames = string.Join("\n", lstItemsWithMalformedIDs);
                        MessageBox.Show(
                            LanguageManager.GetString("Message_NonGuidIdWarning", strLanguage)
                                .Replace("{0}", lstItemsWithMalformedIDs.Count.ToString())
                                .Replace("{1}", strFileName)
                                .Replace("{2}", strMalformedIdNames));
                    }

                    objReference.DuplicatesChecked = true;
                }
            }

            return objDoc;
        }

        /// <summary>
        /// Deep search a document to amend with a new node, returns whether any edits were made.
        /// If Attributes exist for the amending node, the Attributes for the original node will all be overwritten.
        /// </summary>
        /// <param name="objDoc">Document element in which to operate.</param>
        /// <param name="objAmendingNode">The amending (new) node.</param>
        /// <param name="strXPath">The current XPath in the document element that leads to where the amending node would be applied.</param>
        /// <param name="blnHasIdentifier">Whether or not the amending node or any of its children have an identifier element ("id" and/or "name" element). Can safely use a dummy boolean if this is the first call in a recursion.</param>
        private static void AmendNodeChildern(XmlDocument objDoc, XmlNode objAmendingNode, string strXPath)
        {
            // Fetch the old node based on identifiers present in the amending node (id or name)
            string strFilter = string.Empty;
            XmlNode objAmendingNodeId = objAmendingNode["id"];
            if (objAmendingNodeId != null)
            {
                strFilter = "id = \"" + objAmendingNodeId.InnerText.Replace("&amp;", "&") + '\"';
            }
            else
            {
                objAmendingNodeId = objAmendingNode["name"];
                if (objAmendingNodeId != null)
                {
                    strFilter = "name = \"" + objAmendingNodeId.InnerText.Replace("&amp;", "&") + '\"';
                }
            }
            // Child Nodes marked with "isidnode" serve as additional identifier nodes, in case something needs modifying that uses neither a name nor an ID.
            XmlNodeList objAmendingNodeExtraIds = objAmendingNode.SelectNodes("child::*[@isidnode = \"yes\"]");
            foreach (XmlNode objExtraId in objAmendingNodeExtraIds)
            {
                if (!string.IsNullOrEmpty(strFilter))
                    strFilter += " and ";
                strFilter += objExtraId.Name + " = \"" + objExtraId.InnerText.Replace("&amp;", "&") + '\"';
            }

            XmlAttributeCollection objAmendingNodeAttribs = objAmendingNode.Attributes;
            string strCustomXPath = objAmendingNodeAttribs?["xpathfilter"]?.InnerText;
            // We have a custom XPath filter defined for what children to fetch, so add that in.
            if (!string.IsNullOrEmpty(strCustomXPath))
            {
                if (!string.IsNullOrEmpty(strFilter))
                    strFilter += " and ";
                strFilter += '(' + strCustomXPath.Replace("&amp;", "&") + ')';
            }

            if (!string.IsNullOrEmpty(strFilter))
                strFilter = '[' + strFilter + ']';

            string strNewXPath = strXPath + '/' + objAmendingNode.Name + strFilter;
            XmlNodeList objNodesToEdit = objDoc.SelectNodes(strNewXPath);

            // We want to treat nodes that have children elements ("grouping" nodes) differently from those that don't ("data" nodes)
            List<XmlNode> lstElementChildren = new List<XmlNode>();
            if (objAmendingNode.HasChildNodes)
            {
                foreach (XmlNode objChild in objAmendingNode.ChildNodes)
                {
                    if (objChild.NodeType == XmlNodeType.Element)
                    {
                        lstElementChildren.Add(objChild);
                    }
                }
            }

            // Gets the specific operation to execute on this node. If the operation is not supported
            string strOperation = objAmendingNodeAttribs?["amendoperation"]?.InnerText;
            switch (strOperation)
            {
                // These operations are supported
                case "remove":
                case "replace":
                case "append":
                    break;
                case "recurse":
                    // Operation only supported if we have children
                    if (lstElementChildren.Count > 0)
                        break;
                    goto default;
                // If no supported operation is specified, the default is...
                default:
                    // ..."recurse" if we have children...
                    if (lstElementChildren.Count > 0)
                        strOperation = "recurse";
                    // ..."append" if we don't have children and there's no target...
                    else if (objNodesToEdit.Count == 0)
                        strOperation = "append";
                    // ..."replace" if we don't have children and there are one or more targets.
                    else
                        strOperation = "replace";
                    break;
            }
            // Loop through any nodes that satisfy the XPath filter (as long as we have some way of identifying them, the node is a grouping node and not a data node, and/or we wish to remove the node)
            if (objNodesToEdit.Count > 0)
            {
                foreach (XmlNode objNodeToEdit in objNodesToEdit)
                {
                    // If the old node exists and the amending node has the attribute 'amendoperation="remove"', then the old node is completely erased.
                    if (strOperation == "remove")
                    {
                        objNodeToEdit.ParentNode.RemoveChild(objNodeToEdit);
                    }
                    else
                    {
                        XmlAttributeCollection objNodeToEditAttribs = objNodeToEdit.Attributes;
                        switch (strOperation)
                        {
                            case "recurse":
                                // Attributes are the only thing that are altered under "recurse" (they are replaced)
                                if (objNodeToEditAttribs != null && objAmendingNodeAttribs != null && objAmendingNodeAttribs.Count > 0)
                                {
                                    objNodeToEditAttribs.RemoveAll();
                                    foreach (XmlAttribute objNewAttribute in objAmendingNodeAttribs)
                                    {
                                        if (objNewAttribute.Name != "isidnode" && objNewAttribute.Name != "xpathfilter" && objNewAttribute.Name != "amendoperation" && objNewAttribute.Name != "addifnotfound")
                                            objNodeToEditAttribs.Append(objNewAttribute);
                                    }
                                }
                                foreach (XmlNode objChild in lstElementChildren)
                                {
                                    AmendNodeChildern(objDoc, objChild, strNewXPath);
                                }
                                break;
                            case "append":
                                if (objNodeToEditAttribs != null && objAmendingNodeAttribs != null && objAmendingNodeAttribs.Count > 0)
                                {
                                    foreach (XmlAttribute objNewAttribute in objAmendingNodeAttribs)
                                    {
                                        if (objNewAttribute.Name != "isidnode" && objNewAttribute.Name != "xpathfilter" && objNewAttribute.Name != "amendoperation" && objNewAttribute.Name != "addifnotfound")
                                            objNodeToEditAttribs.Append(objNewAttribute);
                                    }
                                }
                                objNodeToEdit.InnerXml += objAmendingNode.InnerXml;
                                break;
                            case "replace":
                                if (objNodeToEditAttribs != null && objAmendingNodeAttribs != null && objAmendingNodeAttribs.Count > 0)
                                {
                                    objNodeToEditAttribs.RemoveAll();
                                    foreach (XmlAttribute objNewAttribute in objAmendingNodeAttribs)
                                    {
                                        if (objNewAttribute.Name != "isidnode" && objNewAttribute.Name != "xpathfilter" && objNewAttribute.Name != "amendoperation" && objNewAttribute.Name != "addifnotfound")
                                            objNodeToEditAttribs.Append(objNewAttribute);
                                    }
                                }
                                objNodeToEdit.InnerXml = objAmendingNode.InnerXml;
                                break;
                        }
                    }
                }
            }
            // If there aren't any old nodes found and the amending node is tagged as needing to be added should this be the case, then append the entire amending node to the XPath.
            else if (strOperation == "append" || ((strOperation == "recurse" || strOperation == "replace") && objAmendingNodeAttribs?["addifnotfound"]?.InnerText != "no"))
            {
                foreach (XmlNode objParentNode in objDoc.SelectNodes(strXPath))
                {
                    XmlNode objNewChildNode = objDoc.ImportNode(objAmendingNode, true);
                    XmlAttributeCollection objNodeToEditAttribs = objNewChildNode.Attributes;
                    objNodeToEditAttribs.RemoveAll();
                    foreach (XmlAttribute objNewAttribute in objAmendingNodeAttribs)
                    {
                        if (objNewAttribute.Name != "isidnode" && objNewAttribute.Name != "xpathfilter" && objNewAttribute.Name != "amendoperation" && objNewAttribute.Name != "addifnotfound")
                            objNewChildNode.Attributes.Append(objNewAttribute);
                    }
                }
            }
        }

        /// <summary>
        /// Verify the contents of the language data translation file.
        /// </summary>
        /// <param name="strLanguage">Language to check.</param>
        /// <param name="lstBooks">List of books.</param>
        public static void Verify(string strLanguage, List<string> lstBooks)
        {
            if (strLanguage == GlobalOptions.DefaultLanguage)
                return;
            XmlDocument objLanguageDoc = new XmlDocument();
            string languageDirectoryPath = Path.Combine(Application.StartupPath, "lang");
            string strFilePath = Path.Combine(languageDirectoryPath, strLanguage + "_data.xml");
            objLanguageDoc.Load(strFilePath);

            string strLangPath = Path.Combine(languageDirectoryPath, "results_" + strLanguage + ".xml");
            FileStream objStream = new FileStream(strLangPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            XmlTextWriter objWriter = new XmlTextWriter(objStream, Encoding.Unicode)
            {
                Formatting = Formatting.Indented,
                Indentation = 1,
                IndentChar = '\t'
            };

            objWriter.WriteStartDocument();
            // <results>
            objWriter.WriteStartElement("results");

            string strPath = Path.Combine(Application.StartupPath, "data");
            foreach (string strFile in Directory.GetFiles(strPath, "*.xml"))
            {
                string strFileName = Path.GetFileName(strFile);

                // Do not bother to check custom files.
                if (!string.IsNullOrEmpty(strFileName) && !strFileName.StartsWith("custom") && !strFile.StartsWith("override") && !strFile.Contains("packs.xml") && !strFile.Contains("ranges.xml"))
                {
                    // Load the current English file.
                    XmlDocument objEnglishDoc = Load(strFileName);
                    XmlNode objEnglishRoot = objEnglishDoc.SelectSingleNode("/chummer");

                    // First pass: make sure the document exists.
                    bool blnExists = false;
                    XmlNode objLanguageRoot = objLanguageDoc.SelectSingleNode("/chummer/chummer[@file = \"" + strFileName + "\"]");
                    if (objLanguageRoot != null)
                        blnExists = true;

                    // <file name="x" exists="y">
                    objWriter.WriteStartElement("file");
                    objWriter.WriteAttributeString("name", strFileName);
                    objWriter.WriteAttributeString("exists", blnExists.ToString());

                    if (blnExists)
                    {
                        foreach (XmlNode objType in objEnglishRoot.ChildNodes)
                        {
                            string strTypeName = objType.Name;
                            objWriter.WriteStartElement(strTypeName);
                            foreach (XmlNode objChild in objType.ChildNodes)
                            {
                                // If the Node has a source element, check it and see if it's in the list of books that were specified.
                                // This is done since not all of the books are available in every language or the user may only wish to verify the content of certain books.
                                bool blnContinue = true;
                                if (objChild["source"] != null)
                                {
                                    blnContinue = false;
                                    foreach (string strBook in lstBooks)
                                    {
                                        if (strBook == objChild["source"].InnerText)
                                        {
                                            blnContinue = true;
                                            break;
                                        }
                                    }
                                }

                                if (blnContinue)
                                {
                                    if (strTypeName != "version" && !((strTypeName == "costs" || strTypeName == "safehousecosts") && strFile.EndsWith("lifestyles.xml")))
                                    {
                                        string strChildName = objChild.Name;
                                        // Look for a matching entry in the Language file.
                                        if (objChild["name"] != null)
                                        {
                                            string strChildNameElement = objChild["name"].InnerText;
                                            XmlNode objNode = objLanguageRoot.SelectSingleNode(strTypeName + "/" + strChildName + "[name = \"" + strChildNameElement + "\"]");
                                            if (objNode != null)
                                            {
                                                // A match was found, so see what elements, if any, are missing.
                                                bool blnTranslate = false;
                                                bool blnAltPage = false;
                                                bool blnAdvantage = false;
                                                bool blnDisadvantage = false;

                                                if (objChild.HasChildNodes)
                                                {
                                                    if (objNode["translate"] != null)
                                                        blnTranslate = true;

                                                    // Do not mark page as missing if the original does not have it.
                                                    if (objChild["page"] != null)
                                                    {
                                                        if (objNode["altpage"] != null)
                                                            blnAltPage = true;
                                                    }
                                                    else
                                                        blnAltPage = true;

                                                    if (strFile.EndsWith("mentors.xml") || strFile.EndsWith("paragons.xml"))
                                                    {
                                                        if (objNode["altadvantage"] != null)
                                                            blnAdvantage = true;
                                                        if (objNode["altdisadvantage"] != null)
                                                            blnDisadvantage = true;
                                                    }
                                                    else
                                                    {
                                                        blnAdvantage = true;
                                                        blnDisadvantage = true;
                                                    }
                                                }
                                                else
                                                {
                                                    blnAltPage = true;
                                                    if (objNode.Attributes?["translate"] != null)
                                                        blnTranslate = true;
                                                }

                                                // At least one pice of data was missing so write out the result node.
                                                if (!blnTranslate || !blnAltPage || !blnAdvantage || !blnDisadvantage)
                                                {
                                                    // <results>
                                                    objWriter.WriteStartElement(strChildName);
                                                    objWriter.WriteAttributeString("exists", "True");
                                                    objWriter.WriteElementString("name", strChildNameElement);
                                                    if (!blnTranslate)
                                                        objWriter.WriteElementString("missing", "translate");
                                                    if (!blnAltPage)
                                                        objWriter.WriteElementString("missing", "altpage");
                                                    if (!blnAdvantage)
                                                        objWriter.WriteElementString("missing", "altadvantage");
                                                    if (!blnDisadvantage)
                                                        objWriter.WriteElementString("missing", "altdisadvantage");
                                                    // </results>
                                                    objWriter.WriteEndElement();
                                                }
                                            }
                                            else
                                            {
                                                // No match was found, so write out that the data item is missing.
                                                // <result>
                                                objWriter.WriteStartElement(strChildName);
                                                objWriter.WriteAttributeString("exists", "False");
                                                objWriter.WriteElementString("name", strChildNameElement);
                                                // </result>
                                                objWriter.WriteEndElement();
                                            }

                                            if (strFileName == "metatypes.xml")
                                            {
                                                if (objChild["metavariants"] != null)
                                                {
                                                    foreach (XmlNode objMetavariant in objChild.SelectNodes("metavariants/metavariant"))
                                                    {
                                                        string strMetavariantName = objMetavariant["name"].InnerText;
                                                        XmlNode objTranslate = objLanguageRoot.SelectSingleNode("metatypes/metatype[name = \"" + strChildNameElement + "\"]/metavariants/metavariant[name = \"" + strMetavariantName + "\"]");
                                                        if (objTranslate != null)
                                                        {
                                                            bool blnTranslate = false;
                                                            bool blnAltPage = false;

                                                            if (objTranslate["translate"] != null)
                                                                blnTranslate = true;
                                                            if (objTranslate["altpage"] != null)
                                                                blnAltPage = true;

                                                            // Item exists, so make sure it has its translate attribute populated.
                                                            if (!blnTranslate || !blnAltPage)
                                                            {
                                                                // <result>
                                                                objWriter.WriteStartElement("metavariants");
                                                                objWriter.WriteStartElement("metavariant");
                                                                objWriter.WriteAttributeString("exists", "True");
                                                                objWriter.WriteElementString("name", strMetavariantName);
                                                                if (!blnTranslate)
                                                                    objWriter.WriteElementString("missing", "translate");
                                                                if (!blnAltPage)
                                                                    objWriter.WriteElementString("missing", "altpage");
                                                                objWriter.WriteEndElement();
                                                                // </result>
                                                                objWriter.WriteEndElement();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // <result>
                                                            objWriter.WriteStartElement("metavariants");
                                                            objWriter.WriteStartElement("metavariant");
                                                            objWriter.WriteAttributeString("exists", "False");
                                                            objWriter.WriteElementString("name", objMetavariant.InnerText);
                                                            objWriter.WriteEndElement();
                                                            // </result>
                                                            objWriter.WriteEndElement();
                                                        }
                                                    }
                                                }
                                            }

                                            if (strFile == "martialarts.xml")
                                            {
                                                if (objChild["techniques"] != null)
                                                {
                                                    foreach (XmlNode objAdvantage in objChild.SelectNodes("techniques/technique"))
                                                    {
                                                        XmlNode objTranslate = objLanguageRoot.SelectSingleNode("martialarts/martialart[name = \"" + strChildNameElement + "\"]/techniques/technique[. = \"" + objAdvantage.InnerText + "\"]");
                                                        if (objTranslate != null)
                                                        {
                                                            // Item exists, so make sure it has its translate attribute populated.
                                                            if (objTranslate.Attributes?["translate"] == null)
                                                            {
                                                                // <result>
                                                                objWriter.WriteStartElement("martialarts");
                                                                objWriter.WriteStartElement("advantage");
                                                                objWriter.WriteAttributeString("exists", "True");
                                                                objWriter.WriteElementString("name", objAdvantage.InnerText);
                                                                objWriter.WriteElementString("missing", "translate");
                                                                objWriter.WriteEndElement();
                                                                // </result>
                                                                objWriter.WriteEndElement();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // <result>
                                                            objWriter.WriteStartElement("martialarts");
                                                            objWriter.WriteStartElement("advantage");
                                                            objWriter.WriteAttributeString("exists", "False");
                                                            objWriter.WriteElementString("name", objAdvantage.InnerText);
                                                            objWriter.WriteEndElement();
                                                            // </result>
                                                            objWriter.WriteEndElement();
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else if (strChildName == "#comment")
                                        {
                                            //Ignore this node, as it's a comment node.
                                        }
                                        else if (!string.IsNullOrEmpty(objChild.InnerText))
                                        {
                                            string strChildInnerText = objChild.InnerText;
                                            // The item does not have a name which means it should have a translate CharacterAttribute instead.
                                            XmlNode objNode =
                                                objLanguageRoot.SelectSingleNode(strTypeName + "/" + strChildName + "[. = \"" + strChildInnerText + "\"]");
                                            if (objNode != null)
                                            {
                                                // Make sure the translate attribute is populated.
                                                if (objNode.Attributes?["translate"] == null)
                                                {
                                                    // <result>
                                                    objWriter.WriteStartElement(strChildName);
                                                    objWriter.WriteAttributeString("exists", "True");
                                                    objWriter.WriteElementString("name", strChildInnerText);
                                                    objWriter.WriteElementString("missing", "translate");
                                                    // </result>
                                                    objWriter.WriteEndElement();
                                                }
                                            }
                                            else
                                            {
                                                // No match was found, so write out that the data item is missing.
                                                // <result>
                                                objWriter.WriteStartElement(strChildName);
                                                objWriter.WriteAttributeString("exists", "False");
                                                objWriter.WriteElementString("name", strChildInnerText);
                                                // </result>
                                                objWriter.WriteEndElement();
                                            }
                                        }
                                    }
                                }
                            }
                            objWriter.WriteEndElement();
                        }

                        // Now loop through the translation file and determine if there are any entries in there that are not part of the base content.
                        foreach (XmlNode objType in objLanguageRoot.ChildNodes)
                        {
                            foreach (XmlNode objChild in objType.ChildNodes)
                            {
                                // Look for a matching entry in the English file.
                                if (objChild["name"] != null)
                                {
                                    string strChildName = objChild.Name;
                                    string strChildNameElement = objChild["name"].InnerText;
                                    XmlNode objNode = objEnglishRoot.SelectSingleNode("/chummer/" + objType.Name + "/" + strChildName + "[name = \"" + strChildNameElement + "\"]");
                                    if (objNode == null)
                                    {
                                        // <noentry>
                                        objWriter.WriteStartElement("noentry");
                                        objWriter.WriteStartElement(strChildName);
                                        objWriter.WriteElementString("name", strChildNameElement);
                                        objWriter.WriteEndElement();
                                        // </noentry>
                                        objWriter.WriteEndElement();
                                    }
                                }
                            }
                        }
                    }

                    // </file>
                    objWriter.WriteEndElement();
                }
            }

            // </results>
            objWriter.WriteEndElement();
            objWriter.WriteEndDocument();
            objWriter.Close();
        }
        #endregion
    }
}
