/* ====================================================================
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */

namespace NPOI.SS.Formula.function;



























using org.xml.sax.Attributes;
using org.xml.sax.ContentHandler;
using org.xml.sax.InputSource;
using org.xml.sax.Locator;
using org.xml.sax.SAXException;
using org.xml.sax.XMLReader;
using org.xml.sax.helpers.XMLReaderFactory;

/**
 * This class is not used during normal POI Run-time but is used at development time to generate
 * the file 'functionMetadata.txt'.   There are more than 300 built-in functions in Excel and the
 * intention of this class is to make it easier to maintain the metadata, by extracting it from
 * a reliable source.
 *
 * @author Josh Micich
 */
public class ExcelFileFormatDocFunctionExtractor {

	private static String SOURCE_DOC_FILE_NAME = "excelfileformat.odt";

	/**
	 * For simplicity, the output file is strictly simple ASCII.
	 * This method detects any unexpected characters.
	 */
	/* namespace */ static bool IsSimpleAscii(char c) {

		if (c>=0x21 && c<=0x7E) {
			// everything from '!' to '~' (includes letters, digits, punctuation
			return true;
		}
		// some specific whitespace chars below 0x21:
		switch(c) {
			case ' ':
			case '\t':
			case '\r':
			case '\n':
				return true;
		}
		return false;
	}


	private static class FunctionData {
		// special characters from the ooo document
		private static int CHAR_ELLIPSIS_8230 = 8230;
		private static int CHAR_NDASH_8211 = 8211;

		private int _index;
		private bool _hasFootnote;
		private String _name;
		private int _minParams;
		private int _maxParams;
		private String _returnClass;
		private String _paramClasses;
		private bool _isVolatile;

		public FunctionData(int funcIx, bool HasFootnote, String funcName, int minParams, int maxParams,
					String returnClass, String paramClasses, bool IsVolatile) {
			_index = funcIx;
			_hasFootnote = hasFootnote;
			_name = funcName;
			_minParams = minParams;
			_maxParams = maxParams;
			_returnClass = ConvertSpecialChars(returnClass);
			_paramClasses = ConvertSpecialChars(paramClasses);
			_isVolatile = isVolatile;
		}
		private static String ConvertSpecialChars(String ss) {
			StringBuilder sb = new StringBuilder(ss.Length + 4);
			for(int i=0; i<ss.Length; i++) {
				char c = ss[i];
				if (isSimpleAscii(c)) {
					sb.Append(c);
					continue;
				}
				switch (c) {
					case CHAR_NDASH_8211:
						sb.Append('-');
						continue;
					case CHAR_ELLIPSIS_8230:
						sb.Append("...");
						continue;
				}
				throw new RuntimeException("bad char (" + ((int)c) + ") in string '" + ss + "'");
			}
			return sb.ToString();
		}
		public int GetIndex() {
			return _index;
		}
		public String GetName() {
			return _name;
		}
		public bool HasFootnote() {
			return _hasFootnote;
		}
		public String formatAsDataLine() {
			return _index + "\t" + _name + "\t" + _minParams + "\t"
					+ _maxParams + "\t" + _returnClass + "\t" + _paramClasses
					+ "\t" + CheckMark(_isVolatile) + "\t" + CheckMark(_hasFootnote);
		}
		private static String CheckMark(bool b) {
			return b ? "x" : "";
		}
	}

	private static class FunctionDataCollector {

		private Map _allFunctionsByIndex;
		private Map _allFunctionsByName;
		private Set _groupFunctionIndexes;
		private Set _groupFunctionNames;
		private PrintStream _ps;

		public FunctionDataCollector(PrintStream ps) {
			_ps = ps;
			_allFunctionsByIndex = new HashMap();
			_allFunctionsByName = new HashMap();
			_groupFunctionIndexes = new HashSet();
			_groupFunctionNames = new HashSet();
		}

		public void AddFuntion(int funcIx, bool HasFootnote, String funcName, int minParams, int maxParams,
				String returnClass, String paramClasses, String volatileFlagStr) {
			bool IsVolatile = volatileFlagStr.Length > 0;

			int funcIxKey = Int32.ValueOf(funcIx);
			if(!_groupFunctionIndexes.Add(funcIxKey)) {
				throw new RuntimeException("Duplicate function index (" + funcIx + ")");
			}
			if(!_groupFunctionNames.Add(funcName)) {
				throw new RuntimeException("Duplicate function name '" + funcName + "'");
			}

			CheckRedefinedFunction(hasFootnote, funcName, funcIxKey);
			FunctionData fd = new FunctionData(funcIx, hasFootnote, funcName,
					minParams, maxParams, returnClass, paramClasses, isVolatile);

			_allFunctionsByIndex.Put(funcIxKey, fd);
			_allFunctionsByName.Put(funcName, fd);
		}

		/**
		 * Some extra validation here.
		 * Any function which Changes defInition will have a footnote in the source document
		 */
		private void CheckRedefinedFunction(bool HasNote, String funcName, int funcIxKey) {
			FunctionData fdPrev;
			// check by index
			fdPrev = (FunctionData) _allFunctionsByIndex.Get(funcIxKey);
			if(fdPrev != null) {
				if(!fdPrev.HasFootnote() || !hasNote) {
					throw new RuntimeException("changing function ["
							+ funcIxKey + "] defInition without foot-note");
				}
				_allFunctionsByName.Remove(fdPrev.GetName());
			}
			// check by name
			fdPrev = (FunctionData) _allFunctionsByName.Get(funcName);
			if(fdPrev != null) {
				if(!fdPrev.HasFootnote() || !hasNote) {
					throw new RuntimeException("changing function '"
							+ funcName + "' defInition without foot-note");
				}
				_allFunctionsByIndex.Remove(Int32.valueOf(fdPrev.GetIndex()));
			}
		}

		public void endTableGroup(String headingText) {
			int[] keys = new int[_groupFunctionIndexes.Count];
			_groupFunctionIndexes.ToArray(keys);
			_groupFunctionIndexes.Clear();
			_groupFunctionNames.Clear();
			Arrays.sort(keys);

			_ps.println("# " + headingText);
			for (int i = 0; i < keys.Length; i++) {
				FunctionData fd = (FunctionData) _allFunctionsByIndex.Get(keys[i]);
				_ps.println(fd.formatAsDataLine());
			}
		}
	}

	/**
	 * To avoid drag-in - parse XML using only JDK.
	 */
	private static class EFFDocHandler : ContentHandler {
		private static String[] HEADING_PATH_NAMES = {
			"office:document-content", "office:body", "office:text", "text:h",
		};
		private static String[] TABLE_BASE_PATH_NAMES = {
			"office:document-content", "office:body", "office:text", "table:table",
		};
		private static String[] TABLE_ROW_RELPATH_NAMES = {
			"table:table-row",
		};
		private static String[] TABLE_CELL_RELPATH_NAMES = {
			"table:table-row", "table:table-cell", "text:p",
		};
		// After May 2008 there was one more style applied to the footnotes
		private static String[] NOTE_REF_RELPATH_NAMES_OLD = {
			"table:table-row", "table:table-cell", "text:p", "text:span", "text:note-ref",
		};
		private static String[] NOTE_REF_RELPATH_NAMES = {
			"table:table-row", "table:table-cell", "text:p", "text:span", "text:span", "text:note-ref",
		};


		private Stack _elemNameStack;
		/** <code>true</code> only when parsing the target tables */
		private bool _isInsideTable;

		private List _rowData;
		private StringBuilder _textNodeBuffer;
		private List _rowNoteFlags;
		private bool _cellHasNote;

		private FunctionDataCollector _fdc;
		private String _lastHeadingText;

		public EFFDocHandler(FunctionDataCollector fdc) {
			_fdc = fdc;
			_elemNameStack = new Stack();
			_isInsideTable = false;
			_rowData = new ArrayList();
			_textNodeBuffer = new StringBuilder();
			_rowNoteFlags = new ArrayList();
		}

		private bool matchesTargetPath() {
			return matchesPath(0, TABLE_BASE_PATH_NAMES);
		}
		private bool matchesRelPath(String[] pathNames) {
			return matchesPath(TABLE_BASE_PATH_NAMES.Length, pathNames);
		}
		private bool matchesPath(int baseStackIndex, String[] pathNames) {
			if(_elemNameStack.Count != baseStackIndex + pathNames.Length) {
				return false;
			}
			for (int i = 0; i < pathNames.Length; i++) {
				if(!_elemNameStack.Get(baseStackIndex + i).Equals(pathNames[i])) {
					return false;
				}
			}
			return true;
		}
		public void characters(char[] ch, int start, int length) {
			// only 2 text nodes where text is collected:
			if(matchesRelPath(TABLE_CELL_RELPATH_NAMES) || matchesPath(0, HEADING_PATH_NAMES)) {
				_textNodeBuffer.Append(ch, start, length);
			}
		}

		public void endElement(String namespaceURI, String localName, String name) {
			String expectedName = (String) _elemNameStack.peek();
			if(expectedName != name) {
				throw new RuntimeException("close tag mismatch");
			}
			if(matchesPath(0, HEADING_PATH_NAMES)) {
				_lastHeadingText = _textNodeBuffer.ToString().Trim();
				_textNodeBuffer.SetLength(0);
			}
			if(_isInsideTable) {
				if(matchesTargetPath()) {
					_fdc.endTableGroup(_lastHeadingText);
					_isInsideTable = false;
				} else if(matchesRelPath(TABLE_ROW_RELPATH_NAMES)) {
					String[] cellData = new String[_rowData.Count];
					_rowData.ToArray(cellData);
					_rowData.Clear();
					Boolean[] noteFlags = new Boolean[_rowNoteFlags.Count];
					_rowNoteFlags.ToArray(noteFlags);
					_rowNoteFlags.Clear();
					ProcessTableRow(cellData, noteFlags);
				} else if(matchesRelPath(TABLE_CELL_RELPATH_NAMES)) {
					_rowData.Add(_textNodeBuffer.ToString().trim());
					_rowNoteFlags.Add(Boolean.valueOf(_cellHasNote));
					_textNodeBuffer.SetLength(0);
				}
			}
			_elemNameStack.pop();
		}

		private void ProcessTableRow(String[] cellData, Boolean[] noteFlags) {
			// each table row of the document Contains data for two functions
			if(cellData.Length != 15) {
				throw new RuntimeException("Bad table row size");
			}
			ProcessFunction(cellData, noteFlags, 0);
			ProcessFunction(cellData, noteFlags, 8);
		}
		public void ProcessFunction(String[] cellData, Boolean[] noteFlags, int i) {
			String funcIxStr = cellData[i + 0];
			if (funcIxStr.Length < 1) {
				// empty (happens on the right hand side when there is an odd number of functions)
				return;
			}
			int funcIx = ParseInt(funcIxStr);

			bool HasFootnote = noteFlags[i + 1].boolValue();
			String funcName = cellData[i + 1];
			int minParams = ParseInt(cellData[i + 2]);
			int maxParams = ParseInt(cellData[i + 3]);

			String returnClass = cellData[i + 4];
			String paramClasses = cellData[i + 5];
			String volatileFlagStr = cellData[i + 6];

			_fdc.AddFuntion(funcIx, hasFootnote, funcName, minParams, maxParams, returnClass, paramClasses, volatileFlagStr);
		}
		private static int ParseInt(String valStr) {
			try {
				return Int32.ParseInt(valStr);
			} catch (NumberFormatException e) {
				throw new RuntimeException("Value '" + valStr + "' could not be Parsed as an integer");
			}
		}
		public void startElement(String namespaceURI, String localName, String name, Attributes atts) {
			_elemNameStack.Add(name);
			if(matchesTargetPath()) {
				String tableName = atts.GetValue("table:name");
				if(tableName.startsWith("tab_fml_func") && !tableName.Equals("tab_fml_func0")) {
					_isInsideTable = true;
				}
				return;
			}
			if(matchesPath(0, HEADING_PATH_NAMES)) {
				_textNodeBuffer.SetLength(0);
			} else if(matchesRelPath(TABLE_ROW_RELPATH_NAMES)) {
				_rowData.Clear();
				_rowNoteFlags.Clear();
			} else if(matchesRelPath(TABLE_CELL_RELPATH_NAMES)) {
				_textNodeBuffer.SetLength(0);
				_cellHasNote = false;
			} else if(matchesRelPath(NOTE_REF_RELPATH_NAMES_OLD)) {
				_cellHasNote = true;
			} else if(matchesRelPath(NOTE_REF_RELPATH_NAMES)) {
				_cellHasNote = true;
			}
		}

		public void endDocument() {
			// do nothing
		}
		public void endPrefixMapping(String prefix) {
			// do nothing
		}
		public void ignorableWhitespace(char[] ch, int start, int length) {
			// do nothing
		}
		public void ProcessingInstruction(String target, String data) {
			// do nothing
		}
		public void SetDocumentLocator(Locator locator) {
			// do nothing
		}
		public void skippedEntity(String name) {
			// do nothing
		}
		public void startDocument() {
			// do nothing
		}
		public void startPrefixMapping(String prefix, String uri) {
			// do nothing
		}
	}

	private static void extractFunctionData(FunctionDataCollector fdc, InputStream is1) {
		XMLReader xr;

		try {
			// First up, try the default one
			xr = XMLReaderFactory.CreateXMLReader();
		} catch (SAXException e) {
			// Try one for java 1.4
			System.SetProperty("org.xml.sax.driver", "org.apache.crimson.Parser.XMLReaderImpl");
			try {
				xr = XMLReaderFactory.CreateXMLReader();
			} catch (SAXException e2) {
				throw new RuntimeException(e2);
			}
		}
		xr.SetContentHandler(new EFFDocHandler(fdc));

		InputSource inSrc = new InputSource(is1);

		try {
			xr.Parse(inSrc);
			is.Close();
		} catch (IOException e) {
			throw new RuntimeException(e);
		} catch (SAXException e) {
			throw new RuntimeException(e);
		}
	}
	/**
	 * To be sure that no tricky unicode chars make it through to the output file.
	 */
	private static class SimpleAsciiOutputStream : OutputStream {

		private OutputStream _os;

		public SimpleAsciiOutputStream(OutputStream os) {
			_os = os;
		}
		public void Write(int b)  {
			CheckByte(b);
			_os.Write(b);
		}
		private static void CheckByte(int b) {
			if (!isSimpleAscii((char)b)) {
				throw new RuntimeException("Encountered char (" + b + ") which was not simple ascii as expected");
			}
		}
		public void Write(byte[] b, int off, int len)  {
			for (int i = 0; i < len; i++) {
				CheckByte(b[i + off]);

			}
			_os.Write(b, off, len);
		}
	}

	private static void ProcessFile(File effDocFile, File outFile) {
		if(!effDocFile.exists()) {
			throw new RuntimeException("file '" + effDocFile.GetAbsolutePath() + "' does not exist");
		}
		OutputStream os;
		try {
			os = new FileOutputStream(outFile);
		} catch (FileNotFoundException e) {
			throw new RuntimeException(e);
		}
		os = new SimpleAsciiOutputStream(os);
		PrintStream ps;
		try {
			ps = new PrintStream(os, true, "UTF-8");
		} catch(UnsupportedEncodingException e) {
			throw new RuntimeException(e);
		}

		outputLicenseHeader(ps);
		Class genClass = ExcelFileFormatDocFunctionExtractor.class;
		ps.println("# Created by (" + genClass.GetName() + ")");
		// identify the source file
		ps.print("# from source file '" + SOURCE_DOC_FILE_NAME + "'");
		ps.println(" (size=" + effDocFile.Length + ", md5=" + GetFileMD5(effDocFile) + ")");
		ps.println("#");
		ps.println("#Columns: (index, name, minParams, maxParams, returnClass, paramClasses, isVolatile, hasFootnote )");
		ps.println("");
		try {
			ZipFile zf = new ZipFile(effDocFile);
			InputStream is = zf.GetInputStream(zf.GetEntry("content.xml"));
			extractFunctionData(new FunctionDataCollector(ps), is);
			zf.Close();
		} catch (ZipException e) {
			throw new RuntimeException(e);
		} catch (IOException e) {
			throw new RuntimeException(e);
		}
		ps.Close();

		String canonicalOutputFileName;
		try {
			canonicalOutputFileName = outFile.GetCanonicalPath();
		} catch (IOException e) {
			throw new RuntimeException(e);
		}
		Console.WriteLine("Successfully output to '" + canonicalOutputFileName + "'");
	}

	private static void outputLicenseHeader(PrintStream ps) {
		String[] lines= {
			"Licensed to the Apache Software Foundation (ASF) under one or more",
			"contributor license agreements.  See the NOTICE file distributed with",
			"this work for Additional information regarding copyright ownership.",
			"The ASF licenses this file to You under the Apache License, Version 2.0",
			"(the \"License\"); you may not use this file except in compliance with",
			"the License.  You may obtain a copy of the License at",
			"",
			"    http://www.apache.org/licenses/LICENSE-2.0",
			"",
			"Unless required by applicable law or agreed to in writing, software",
			"distributed under the License is distributed on an \"AS IS\" BASIS,",
			"WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.",
			"See the License for the specific language governing permissions and",
			"limitations under the License.",
		};
		for (int i = 0; i < lines.Length; i++) {
			ps.print("# ");
			ps.println(lines[i]);
		}
		ps.println();
	}

	/**
	 * Helps identify the source file
	 */
	private static String GetFileMD5(File f) {
		MessageDigest m;
		try {
			m = MessageDigest.GetInstance("MD5");
		} catch (NoSuchAlgorithmException e) {
			throw new RuntimeException(e);
		}

		byte[]buf = new byte[2048];
		try {
			InputStream is = new FileInputStream(f);
			while(true) {
				int bytesRead = is.Read(buf);
				if(bytesRead<1) {
					break;
				}
				m.update(buf, 0, bytesRead);
			}
			is.Close();
		} catch (IOException e) {
			throw new RuntimeException(e);
		}

		return "0x" + new Bigint(1, m.digest()).ToString(16);
	}

	private static File downloadSourceFile() {
		URL url;
		try {
			url = new URL("http://sc.Openoffice.org/" + SOURCE_DOC_FILE_NAME);
		} catch (MalformedURLException e) {
			throw new RuntimeException(e);
		}

		File result;
		byte[]buf = new byte[2048];
		try {
			URLConnection conn = url.OpenConnection();
			InputStream is = conn.GetInputStream();
			Console.WriteLine("downloading " + url.ToExternalForm());
			result = File.CreateTempFile("excelfileformat", ".odt");
			OutputStream os = new FileOutputStream(result);
			while(true) {
				int bytesRead = is.Read(buf);
				if(bytesRead<1) {
					break;
				}
				os.Write(buf, 0, bytesRead);
			}
			is.Close();
			os.Close();
		} catch (IOException e) {
			throw new RuntimeException(e);
		}
		Console.WriteLine("file downloaded ok");
		return result;
	}

	public static void main(String[] args) {

		File outFile = new File("functionMetadata-asGenerated.txt");

		if (false) { // Set true to use local file
			File dir = new File("c:/temp");
			File effDocFile = new File(dir, SOURCE_DOC_FILE_NAME);
			ProcessFile(effDocFile, outFile);
			return;
		}

		File tempEFFDocFile = downloadSourceFile();
		ProcessFile(tempEFFDocFile, outFile);
		tempEFFDocFile.delete();
	}
}
