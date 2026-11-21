const vscode = require('vscode');
const { spawn } = require('child_process');
const path = require('path');
const starter = ['style (','  main:','    font = Arial','    background = #20202A','    color = #F4F7FB',')','','group = main (','  text(hl) = ${1:My TXL app}','  text = ${2:Welcome}','  ${0}',')'].join('\n');
const elementCompletions = [
  ['text', 'text = ', 'Add a text element'], ['button', 'button = ${1:Label}', 'Add a button'],
  ['image', 'image = ${1:assets/image.png}', 'Add an image asset'], ['input', 'input = ', 'Add a text input'],
  ['checkbox', 'checkbox = ', 'Add a checkbox'], ['line', 'line', 'Add a separator'],
  ['space', 'space = ', 'Add vertical space'], ['group', 'group = ${1:name} (\n  ${0}\n)', 'Open a layout group']
];
const styleProperties = [
  ['color','color = ','Set foreground color'],['background','background = ','Set background color'],['font','font = ','Set the font family'],
  ['size','size = ','Set font size'],['weight','weight = ','Set font weight'],['margin','margin = ','Set outer spacing'],['padding','padding = ','Set inner spacing'],
  ['width','width = ','Set control width'],['height','height = ','Set control height'],['opacity','opacity = ','Set opacity']
];
const googleFonts = ['Roboto','Open Sans','Lato','Montserrat','Poppins','Inter','Oswald','Raleway','Nunito','Roboto Condensed','Playfair Display','Merriweather','Ubuntu','Rubik','Work Sans','DM Sans','Source Sans 3','PT Sans','Noto Sans','Fira Sans','Manrope','Quicksand','Mulish','Barlow','Karla','Cabin','Libre Franklin','Josefin Sans','Space Grotesk','Outfit','Plus Jakarta Sans','IBM Plex Sans','Inconsolata','Roboto Mono','Fira Code','Source Code Pro','JetBrains Mono','Space Mono','Bebas Neue','Comfortaa','Dancing Script','Pacifico','Lobster','Abril Fatface','Archivo','Arimo','Asap','Barlow Condensed','Bitter','Cairo','Crimson Text','DM Serif Display','Heebo','Jost','Kanit','Lexend','Libre Baskerville','Lora','Maven Pro','Mukta','Nanum Gothic','Newsreader','Oxygen','Prompt','Sora','Titillium Web','Urbanist','Varela Round','Yanone Kaffeesatz'];
const styleValues = {layout:['vertical','horizontal'],color:['#20202A','#F4F7FB','#000000','#FFFFFF','black','white','red','blue','green','transparent'],background:['#20202A','#F4F7FB','#000000','#FFFFFF','black','white','transparent']};
function isInsideStyle(document, lineNumber) {
  let open = false;
  for (let i = 0; i <= lineNumber; i++) {
    const text = document.lineAt(i).text.trim();
    if (/^style\s*\(/i.test(text)) open = true;
    if (open && text === ')') open = false;
  }
  return open;
}
function makeItems(rows, wordRange) {
  return rows.map(([label, body, detail]) => {
    const item = new vscode.CompletionItem(label, vscode.CompletionItemKind.Snippet);
    item.detail = detail; item.filterText = label; item.sortText = `0_${label}`;
    item.insertText = new vscode.SnippetString(body);
    if (wordRange) item.range = wordRange;
    return item;
  });
}
function activate(context) {
  const selector = { language: 'txl', scheme: 'file' };
  context.subscriptions.push(vscode.commands.registerCommand('txl.preview', () => {
    const editor = vscode.window.activeTextEditor;
    const activeFile = editor?.document.languageId === 'txl' ? editor.document.uri.fsPath : undefined;
    const projectDir = activeFile ? path.dirname(activeFile) : vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
    if (!projectDir) { vscode.window.showErrorMessage('Open a TXL file or project folder before starting the TXL preview.'); return; }
    const output = vscode.window.createOutputChannel('TXL Preview');
    output.appendLine(`Running: txl preview "${projectDir}"`); output.show(true);
    const child = spawn('txl', ['preview', projectDir], { cwd: projectDir, windowsHide: true });
    child.on('error', error => {
      const message = error.code === 'ENOENT' ? 'TXL preview could not start: the `txl` CLI was not found on PATH. Install/configure the CLI and restart VS Code.' : `TXL preview could not start: ${error.message}`;
      vscode.window.showErrorMessage(message); output.appendLine(`Unable to launch txl: ${error.message}`);
    });
    child.stdout?.on('data', data => output.append(data.toString()));
    child.stderr?.on('data', data => output.append(data.toString()));
    child.on('close', code => output.appendLine(`TXL preview exited with code ${code}.`));
  }));
  context.subscriptions.push(vscode.languages.registerCompletionItemProvider(selector, {
    provideCompletionItems(document, position) {
      const line = document.lineAt(position.line).text;
      const beforeCursor = line.slice(0, position.character);
      if (beforeCursor.trim() === '!') {
        const item = new vscode.CompletionItem('TXL application starter', vscode.CompletionItemKind.Snippet);
        item.detail = 'Insert a minimal TXL app structure'; item.filterText = '!'; item.sortText = '0_starter';
        item.range = new vscode.Range(position.line, Math.max(0, position.character - 1), position.line, position.character);
        item.insertText = new vscode.SnippetString(starter); return [item];
      }
      if (/^\s*text(?:\([^)]*\))?\s*=\s*/i.test(beforeCursor)) return [];
      const propertyValue = beforeCursor.match(/^\s*(layout|color|background|font)\s*=\s*(.*)$/i);
      if (propertyValue) {
        const property = propertyValue[1].toLowerCase();
        const start = beforeCursor.lastIndexOf('=') + 1;
        const range = new vscode.Range(position.line, start, position.line, position.character);
        if (property === 'font') {
          const partial = propertyValue[2].trim().toLowerCase();
          return googleFonts.filter(font => !partial || font.toLowerCase().startsWith(partial)).map(font => {
            const item = new vscode.CompletionItem(font, vscode.CompletionItemKind.Value);
            item.detail = 'Google Fonts family'; item.range = range; item.insertText = font; return item;
          });
        }
        const partial = propertyValue[2].trim().toLowerCase();
        return (styleValues[property] || []).filter(value => !partial || value.toLowerCase().startsWith(partial)).map(value => {
          const item = new vscode.CompletionItem(value, vscode.CompletionItemKind.Value);
          item.detail = `Value for ${property}`; item.range = range; item.insertText = value; return item;
        });
      }
      const wordRange = document.getWordRangeAtPosition(position);
      const prefix = wordRange ? document.getText(wordRange).toLowerCase() : '';
      const rows = isInsideStyle(document, position.line) ? styleProperties : elementCompletions;
      return makeItems(rows.filter(([label]) => !prefix || label.toLowerCase().startsWith(prefix)), wordRange);
    }
  }, '!'));
  context.subscriptions.push(vscode.languages.registerHoverProvider(selector, {
    provideHover(document, position) {
      const range = document.getWordRangeAtPosition(position);
      if (!range) return undefined;
      const word = document.getText(range);
      const help = {
        group: 'A group holds controls. `layout = vertical` stacks them top-to-bottom; `horizontal` places them side-by-side.',
        style: 'Opens style definitions. A section uses `main:`; close the style block with `)`. ',
        text: 'Creates a text label. Text after `text =` is content, not a TXL command position.',
        button: 'Creates a native WPF button.',
        layout: '`vertical` stacks controls top-to-bottom; `horizontal` places them side-by-side.'
      }[word];
      return help ? new vscode.Hover(new vscode.MarkdownString(help)) : undefined;
    }
  }));
  context.subscriptions.push(vscode.languages.registerDocumentFormattingEditProvider(selector, {
    provideDocumentFormattingEdits(document) {
      let depth = 0, inColonStyle = false;
      const edits = [];
      for (let index = 0; index < document.lineCount; index++) {
        const line = document.lineAt(index), text = line.text.trim();
        if (!text) continue;
        const styleHeading = /^[A-Za-z][\w-]*:$/.test(text);
        if (text === ')') { depth = Math.max(0, depth - 1); inColonStyle = false; }
        const formatted = '  '.repeat(depth + (inColonStyle && !styleHeading ? 1 : 0)) + text;
        if (line.text !== formatted) edits.push(vscode.TextEdit.replace(line.range, formatted));
        if (/\($/.test(text)) depth++;
        if (styleHeading) inColonStyle = true;
      }
      return edits;
    }
  }));
}
function deactivate() {}
module.exports = { activate, deactivate };
