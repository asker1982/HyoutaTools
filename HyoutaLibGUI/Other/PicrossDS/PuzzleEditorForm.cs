﻿using HyoutaTools.Other.PicrossDS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HyoutaLibGUI.Other.PicrossDS {
	public partial class PuzzleEditorForm : Form {
		private SaveFile Save;
		private String OriginalFilename;
		private bool PuzzleLoaded = false;

		public PuzzleEditorForm() {
			InitializeComponent();

			if ( !LoadSave() ) {
				this.Close();
			}
		}

		private bool LoadSave() {
			OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
			dialog.Filter = "NDS Save (*.sav, *.dsv)|*.sav;*.dsv|Any File|*.*";
			DialogResult result = dialog.ShowDialog();
			if ( result == DialogResult.OK ) {
				this.SuspendLayout();
				OriginalFilename = dialog.FileName;
				Save = new SaveFile( OriginalFilename );
				foreach ( var puzzle in Save.ClassicPuzzles ) {
					comboBoxPuzzleSlot.Items.Add( puzzle );
				}
				foreach ( var puzzle in Save.OriginalPuzzles ) {
					comboBoxPuzzleSlot.Items.Add( puzzle );
				}
				comboBoxPuzzleSlot.SelectedIndex = 0;

				this.ResumeLayout( true );

				return true;
			}

			return false;
		}

		void PopulateGuiWithPuzzle( ClassicPuzzle puzzle ) {
			PuzzleLoaded = false;

			try {
				comboBoxPuzzleDimensions.SelectedIndex = ( puzzle.Width / 5 ) - 1; // there's only five valid dimensions, this should always get the right one
			} catch ( ArgumentOutOfRangeException ) {
				comboBoxPuzzleDimensions.SelectedIndex = 0;
			}

			checkBoxFreeMode.Checked = puzzle.Mode == 0x02;
			checkBoxCleared.Checked = ( puzzle.Unknown2 & 0x01 ) == 0x00;
			textBoxCleartime.Text = puzzle.ClearTime.ToString();
			textBoxName.Text = puzzle.PuzzleName.Trim( '\0' );
			textBoxPack.Text = puzzle.PackName.Trim( '\0' );

			try {
				comboBoxPackLetter.SelectedIndex = puzzle.PackLetter;
			} catch ( ArgumentOutOfRangeException ) {
				comboBoxPackLetter.SelectedIndex = 0;
			}
			try {
				comboBoxPackNumber.SelectedIndex = puzzle.PackNumber;
			} catch ( ArgumentOutOfRangeException ) {
				comboBoxPackNumber.SelectedIndex = 0;
			}

			PuzzleLoaded = true;
		}

		private void WriteGuiPuzzleDataToSave( object sender, EventArgs e ) {
			if ( !PuzzleLoaded ) return;
			var puzzle = (ClassicPuzzle)comboBoxPuzzleSlot.SelectedItem;

			var dimensionsString = comboBoxPuzzleDimensions.Text.Split( 'x' );
			puzzle.Width = Byte.Parse( dimensionsString[0] );
			puzzle.Height = Byte.Parse( dimensionsString[1] );

			puzzle.Mode = (byte)( checkBoxFreeMode.Checked ? 0x02 : 0x01 );
			puzzle.ClearTime = UInt32.Parse( textBoxCleartime.Text );
			if ( checkBoxCleared.Checked ) {
				// seriously, C#? I can't invert a byte in a single statement?
				byte tmp = 0x01; puzzle.Unknown2 &= (byte)~tmp;
			} else {
				puzzle.Unknown2 |= 0x01;
			}
			puzzle.PuzzleName = textBoxName.Text;
			puzzle.PackName = textBoxPack.Text;
			puzzle.PackLetter = (byte)comboBoxPackLetter.SelectedIndex;
			puzzle.PackNumber = (byte)comboBoxPackNumber.SelectedIndex;

			RefreshPuzzleSlotText();

			return;
		}

		private void RefreshPuzzleSlotText() {
			// bleeeh this is ugly
			try {
				typeof( ComboBox ).InvokeMember( "RefreshItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.InvokeMethod, null, comboBoxPuzzleSlot, new object[] { } );
			} catch ( Exception ) { } // if it fails (maybe someone changes the internal functions in the future) just do nothing, it's not *that* important to refresh the text
		}

		private void comboBoxPuzzleSlot_SelectedIndexChanged( object sender, EventArgs e ) {
			PopulateGuiWithPuzzle( (ClassicPuzzle)comboBoxPuzzleSlot.SelectedItem );
		}

		private bool IsSelectedPuzzleOriginalPuzzle {
			get {
				return comboBoxPuzzleSlot.SelectedIndex >= 100;
			}
		}

		private void buttonExport_Click( object sender, EventArgs e ) {
			var puzzle = (ClassicPuzzle)comboBoxPuzzleSlot.SelectedItem;
			SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
			dialog.Filter = "Picross Puzzle (*.picross)|*.picross|Any File|*.*";
			dialog.FileName = puzzle.ToString() + ".picross";
			DialogResult result = dialog.ShowDialog();
			if ( result == DialogResult.OK ) {
				var outfile = new byte[IsSelectedPuzzleOriginalPuzzle ? 0x7C8 : 0xC0];
				puzzle.Write( outfile, 0x0 );
				System.IO.File.WriteAllBytes( dialog.FileName, outfile );
			}
		}

		private void buttonImport_Click( object sender, EventArgs e ) {
			OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
			dialog.Filter = "Picross Puzzle (*.picross)|*.picross|Any File|*.*";
			DialogResult result = dialog.ShowDialog();
			if ( result == DialogResult.OK ) {
				var infile = System.IO.File.ReadAllBytes( dialog.FileName );

				if ( IsSelectedPuzzleOriginalPuzzle ) {
					var puzzle = new OriginalPuzzle( infile, 0x0 );
					puzzle.Type = 0x03;
					Save.OriginalPuzzles[comboBoxPuzzleSlot.SelectedIndex - 100] = puzzle;
					comboBoxPuzzleSlot.Items[comboBoxPuzzleSlot.SelectedIndex] = puzzle;
					PopulateGuiWithPuzzle( puzzle );
				} else {
					var puzzle = new ClassicPuzzle( infile, 0x0 );
					puzzle.Type = 0x02;
					Save.ClassicPuzzles[comboBoxPuzzleSlot.SelectedIndex] = puzzle;
					comboBoxPuzzleSlot.Items[comboBoxPuzzleSlot.SelectedIndex] = puzzle;
					PopulateGuiWithPuzzle( puzzle );
				}

				WriteGuiPuzzleDataToSave( sender, e );
			}
		}

		private void buttonSave_Click( object sender, EventArgs e ) {
			Save.WriteFile( OriginalFilename );
		}

		private void buttonSaveAs_Click( object sender, EventArgs e ) {
			SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
			dialog.Filter = "NDS Save (*.sav, *.dsv)|*.sav;*.dsv|Any File|*.*";
			dialog.FileName = System.IO.Path.GetFileName( OriginalFilename );
			DialogResult result = dialog.ShowDialog();
			if ( result == DialogResult.OK ) {
				OriginalFilename = dialog.FileName;
				Save.WriteFile( OriginalFilename );
			}
		}
	}
}
