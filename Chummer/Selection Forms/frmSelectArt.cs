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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace Chummer
{
    public partial class frmSelectArt : Form
    {
        private string _strSelectedItem = string.Empty;

        private Mode _objMode = Mode.Art;
        private string _strNode = "art";
        private string _strRoot = "arts";
        private string _strCategory = string.Empty;
        private string _strLocalName = string.Empty;
        private readonly Character _objCharacter;

        private readonly XmlDocument _objXmlDocument = null;

        private readonly XmlDocument _objMetamagicDocument = null;
        private readonly XmlDocument _objSpellDocument = null;
        private readonly XmlDocument _objPowerDocument = null;
        private readonly XmlDocument _objQualityDocument = null;

        public enum Mode
        {
            Art = 0,
            Enhancement,
            Enchantment,
            Ritual,
        }

        public frmSelectArt(Character objCharacter, Mode objWindowMode)
        {
            InitializeComponent();
            LanguageManager.TranslateWinForm(GlobalOptions.Language, this);
            _objCharacter = objCharacter;

            _objMetamagicDocument = XmlManager.Load("metamagic.xml");
            _objSpellDocument = XmlManager.Load("spells.xml");
            _objPowerDocument = XmlManager.Load("powers.xml");
            _objQualityDocument = XmlManager.Load("qualities.xml");

            // Load the Metamagic information.
            WindowMode = objWindowMode;
            _objXmlDocument = _objSpellDocument;
            switch (_objMode)
            {
                case Mode.Art:
                    _objXmlDocument = _objMetamagicDocument;
                    break;
                case Mode.Enhancement:
                    _objXmlDocument = _objPowerDocument;
                    break;
            }
        }

        private void frmSelectArt_Load(object sender, EventArgs e)
        {
            // Update the window title if needed.
            
            switch (_objMode)
            {
                case Mode.Enhancement:
                    _strLocalName = LanguageManager.GetString("String_Enhancement", GlobalOptions.Language);
                    break;
                case Mode.Enchantment:
                    _strLocalName = LanguageManager.GetString("String_Enchantment", GlobalOptions.Language);
                    break;
                case Mode.Ritual:
                    _strLocalName = LanguageManager.GetString("String_Ritual", GlobalOptions.Language);
                    break;
                case Mode.Art:
                    _strLocalName = LanguageManager.GetString("String_Art", GlobalOptions.Language);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            Text = LanguageManager.GetString("Title_SelectGeneric", GlobalOptions.Language).Replace("{0}", _strLocalName);
            chkLimitList.Text = LanguageManager.GetString("Checkbox_SelectGeneric_LimitList", GlobalOptions.Language).Replace("{0}", _strLocalName);

            foreach (Label objLabel in Controls.OfType<Label>())
            {
                if (objLabel.Text.StartsWith('['))
                    objLabel.Text = string.Empty;
            }

            BuildList();
        }

        private void lstArt_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(lstArt.Text))
                return;

            // Retireve the information for the selected piece of Cyberware.
            XmlNode objXmlMetamagic = _objXmlDocument.SelectSingleNode("/chummer/" + _strRoot + "/" + _strNode + "[name = \"" + lstArt.SelectedValue + "\"]");

            string strBook = CommonFunctions.LanguageBookShort(objXmlMetamagic["source"]?.InnerText, GlobalOptions.Language);
            string strPage = objXmlMetamagic["altpage"]?.InnerText ?? objXmlMetamagic["page"]?.InnerText;
            lblSource.Text = $"{strBook} {strBook}";

            tipTooltip.SetToolTip(lblSource, CommonFunctions.LanguageBookLong(objXmlMetamagic["source"].InnerText, GlobalOptions.Language) + " " + LanguageManager.GetString("String_Page", GlobalOptions.Language) + " " + strPage);
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            AcceptForm();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void lstArt_DoubleClick(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(lstArt.Text))
                AcceptForm();
        }

        private void chkLimitList_CheckedChanged(object sender, EventArgs e)
        {
            BuildList();
        }

        #region Properties
        /// <summary>
        /// Set the window's Mode to Art, Enchantment, Enhancement, or Ritual.
        /// </summary>
        public Mode WindowMode
        {
            get
            {
                return _objMode;
            }
            set
            {
                _objMode = value;
                switch (_objMode)
                {
                    case Mode.Art:
                        _strNode = "art";
                        _strRoot = "arts";
                        break;
                    case Mode.Enchantment:
                        _strNode = "spell";
                        _strRoot = "spells";
                        _strCategory = "Enchantments";
                        break;
                    case Mode.Enhancement:
                        _strNode = "enhancement";
                        _strRoot = "enhancements";
                        break;
                    case Mode.Ritual:
                        _strNode = "spell";
                        _strRoot = "spells";
                        _strCategory = "Rituals";
                        break;
                }
            }
        }

        /// <summary>
        /// Name of Metamagic that was selected in the dialogue.
        /// </summary>
        public string SelectedItem
        {
            get
            {
                return _strSelectedItem;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Build the list of Metamagics.
        /// </summary>
        private void BuildList()
        {
            XmlNodeList objXmlMetamagicList;
            List<ListItem> lstArts = new List<ListItem>();

            // Load the Metamagic information.
            switch (_objMode)
            {
                case Mode.Art:
                case Mode.Enhancement:
                    objXmlMetamagicList = _objXmlDocument.SelectNodes("/chummer/" + _strRoot + "/" + _strNode + "[" + _objCharacter.Options.BookXPath() + "]");
                    break;
                default:
                    objXmlMetamagicList = _objXmlDocument.SelectNodes("/chummer/" + _strRoot + "/" + _strNode + "[category = '" + _strCategory + "' and (" + _objCharacter.Options.BookXPath() + ")]");
                    break;
            }

            foreach (XmlNode objXmlMetamagic in objXmlMetamagicList)
            {
                if (!chkLimitList.Checked || Backend.SelectionShared.RequirementsMet(objXmlMetamagic, false, _objCharacter, null, null, _objQualityDocument, string.Empty, _strLocalName))
                {
                    string strName = objXmlMetamagic["name"].InnerText;
                    lstArts.Add(new ListItem(strName, objXmlMetamagic["translate"]?.InnerText ?? strName));
                }
            }
            lstArts.Sort(CompareListItems.CompareNames);
            lstArt.BeginUpdate();
            lstArt.DataSource = null;
            lstArt.ValueMember = "Value";
            lstArt.DisplayMember = "Name";
            lstArt.DataSource = lstArts;
            lstArt.EndUpdate();
        }

        /// <summary>
        /// Accept the selected item and close the form.
        /// </summary>
        private void AcceptForm()
        {
            if (string.IsNullOrEmpty(lstArt.Text))
                return;

            _strSelectedItem = lstArt.SelectedValue.ToString();

            // Make sure the selected Metamagic or Echo meets its requirements.
            XmlNode objXmlMetamagic;
            if (_objMode == Mode.Art)
                objXmlMetamagic = _objXmlDocument.SelectSingleNode("/chummer/arts/art[name = \"" + lstArt.SelectedValue + "\"]");
            else if (_objMode == Mode.Enchantment)
                objXmlMetamagic = _objXmlDocument.SelectSingleNode("/chummer/spells/spell[category = \"Enchantments\" and name = \"" + lstArt.SelectedValue + "\"]");
            else if (_objMode == Mode.Enhancement)
                objXmlMetamagic = _objXmlDocument.SelectSingleNode("/chummer/enhancements/enhancement[name = \"" + lstArt.SelectedValue + "\"]");
            else
                objXmlMetamagic = _objXmlDocument.SelectSingleNode("/chummer/spells/spell[category = \"Rituals\" and name = \"" + lstArt.SelectedValue + "\"]");

            if (!Backend.SelectionShared.RequirementsMet(objXmlMetamagic, true, _objCharacter, null, null, _objQualityDocument, string.Empty, _strLocalName))
                return;

            DialogResult = DialogResult.OK;
        }


        #endregion

        private void lblSource_Click(object sender, EventArgs e)
        {
            CommonFunctions.OpenPDF(lblSource.Text);
        }
    }
}
