const vscode = require('vscode');
const { spawn } = require('child_process');
const path = require('path');

const starter = [
  'window (',
  '  title: ${1:My TXL app}',
  '  size: ${2:1100x760}',
  '  mode: ${3:normal}',
  '  # Optional: icon: assets/app.ico (add that file to your project first)',
  ')',
  '',
  'style (',
  '  main:',
  '    font = Arial',
  '    background = #20202A',
  '    color = #F4F7FB',
  '  button:',
  '    background = #0EA5E9',
  '    color = #FFFFFF',
  ')',
  '',
  'actions (',
  '  welcomeButton(click):',
  '    alert = TXL is running.',
  ')',
  '',
  'group = main (',
  '  text.headline = ${4:My TXL app}',
  '  text = ${5:A native Windows desktop app}',
  '  button(welcomeButton) = Show a message',
  '  ${0}',
  ')'
].join('\n');

const elementCompletions = [
  ['text', 'text = ${1:Label}', 'Add a text label'],
  ['text.headline', 'text.headline = ${1:Headline}', 'Add a large headline'],
  ['button', 'button = ${1:Label}', 'Add a button'],
  ['image', 'image = ${1:assets/image.png}', 'Show a local image or HTTP(S) URL'],
  ['video', 'video = ${1:assets/video.mp4}', 'Play a local video or HTTP(S) URL'],
  ['input', 'input = ${1:Placeholder}', 'Add an input with placeholder text'],
  ['checkbox', 'checkbox = ${1:Label}', 'Add a checkbox'],
  ['list', 'list = ${1:tasks}', 'Show a persistent todo list'],
  ['line', 'line', 'Add a separator'],
  ['space', 'space = ${1:20}', 'Add vertical space'],
  ['group', 'group = ${1:name} (\n  ${0}\n)', 'Open a layout group']
];

const styleProperties = [
  ['color', 'color = ${1:#F4F7FB}', 'Set foreground color'],
  ['background', 'background = ${1:#20202A}', 'Set background color'],
  ['font', 'font = ${1:Arial}', 'Set the font family'],
  ['size', 'size = ${1:16}', 'Set font size or image size'],
  ['weight', 'weight = ${1:bold}', 'Set font weight'],
  ['margin', 'margin = ${1:4}', 'Set outer spacing'],
  ['padding', 'padding = ${1:8}', 'Set inner spacing'],
  ['width', 'width = ${1:320}', 'Set width'],
  ['height', 'height = ${1:180}', 'Set height'],
  ['position', 'position = x${1:18} y${2:3}', 'Position inside an absolute Canvas'],
  ['opacity', 'opacity = ${1:1}', 'Set opacity']
];

const windowProperties = [
  ['title', 'title: ${1:My app}', 'Set the title bar label'],
  ['icon', 'icon: ${1:assets/app.ico}', 'Project-relative window icon path'],
  ['size', 'size: ${1:1100x760}', 'Set window size as WIDTHxHEIGHT'],
  ['mode', 'mode: ${1|normal,maximized,fullscreen,borderless|}', 'Select the window mode']
];

const googleFonts = ['Roboto','Open Sans','Lato','Montserrat','Poppins','Inter','Oswald','Raleway','Nunito','Roboto Condensed','Playfair Display','Merriweather','Ubuntu','Rubik','Work Sans','DM Sans','Source Sans 3','PT Sans','Noto Sans','Fira Sans','Manrope','Quicksand','Mulish','Barlow','Karla','Cabin','Libre Franklin','Josefin Sans','Space Grotesk','Outfit','Plus Jakarta Sans','IBM Plex Sans','Inconsolata','Roboto Mono','Fira Code','Source Code Pro','JetBrains Mono','Space Mono','Bebas Neue','Comfortaa','Dancing Script','Pacifico','Lobster','Abril Fatface','Archivo','Arimo','Asap','Barlow Condensed','Bitter','Cairo','Crimson Text','DM Serif Display','Heebo','Jost','Kanit','Lexend','Libre Baskerville','Lora','Maven Pro','Mukta','Nanum Gothic','Newsreader','Oxygen','Prompt','Sora','Titillium Web','Urbanist','Varela Round','Yanone Kaffeesatz'];
const styleValues = {
  layout: ['vertical', 'horizontal', 'absolute'],
  color: ['#20202A', '#F4F7FB', '#000000', '#FFFFFF', 'black', 'white', 'red', 'blue', 'green', 'transparent'],
  background: ['#20202A', '#F4F7FB', '#000000', '#FFFFFF', 'black', 'white', 'transparent'],
  size: ['12', '14', '16', '20', '24', '32', '48'],
  width: ['120', '180', '240', '320', '480'],
  height: ['120', '180', '240', '320'],
  weight: ['normal', 'bold'],
  position: ['x18 y3', 'x0 y0', 'x100 y100']
};

function isInsideBlock(document, lineNumber, blockName) {
  let open = false;
  for (let i = 0; i <= lineNumber; i++) {
    const text = document.lineAt(i).text.trim();
    if (new RegExp(`^${blockName}\\s*\\(`, 'i').test(text)) open = true;
    if (open && text === ')') open = false;
  }
  return open;
}

function makeItems(rows, range) {
  return rows.map(([label, body, detail]) => {
    const item = new vscode.CompletionItem(label, vscode.CompletionItemKind.Snippet);
    item.detail = detail;
    item.filterText = label;
    item.sortText = `0_${label}`;
    item.insertText = new vscode.SnippetString(body);
    if (range) item.range = range;
    return item;
  });
}

function sourceClasses(document) {
  const classes = new Map();
  const element = /^\s*(text|button|image|video|input|checkbox|list|line|space)\(([A-Za-z][\w-]*)\)/i;
  for (let i = 0; i < document.lineCount; i++) {
    const match = document.lineAt(i).text.match(element);
    if (!match) continue;
    const name = match[2];
    if (!classes.has(name.toLowerCase())) {
      const body = match[1].toLowerCase() === 'image'
        ? `${name}:\n  size = ${'${1:160}'}`
        : `${name}:\n  color = ${'${1:#F4F7FB}'}`;
      classes.set(name.toLowerCase(), [name + ':', body, `Create a style section for class '${name}'`]);
    }
  }
  return [...classes.values()];
}

async function mediaItems(document, position, beforeCursor, type) {
  const range = new vscode.Range(position.line, beforeCursor.lastIndexOf('=') + 1, position.line, position.character);
  const items = [];
  const folders = vscode.workspace.workspaceFolders || [];
  if (folders.length) {
    const filters = type === 'video' ? '**/*.{mp4,m4v,mov,wmv,avi,webm}' : '**/*.{png,jpg,jpeg,gif,bmp,webp,ico}';
    const files = await vscode.workspace.findFiles(filters, '**/{node_modules,.git,dist,.txl-generated}/**', 100);
    for (const file of files) {
      const root = folders.find(folder => file.fsPath.startsWith(folder.uri.fsPath + path.sep)) || folders[0];
      const relative = path.relative(root.uri.fsPath, file.fsPath).split(path.sep).join('/');
      const item = new vscode.CompletionItem(relative, vscode.CompletionItemKind.File);
      item.detail = 'Project media asset'; item.range = range; item.insertText = relative;
      items.push(item);
    }
  }
  const remote = new vscode.CompletionItem('https:// media URL', vscode.CompletionItemKind.Value);
  remote.detail = 'Remote media must use HTTP or HTTPS'; remote.range = range;
  remote.insertText = new vscode.SnippetString('https://${1:example.com/media.' + (type === 'video' ? 'mp4' : 'png') + '}');
  items.push(remote);
  return items;
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
    async provideCompletionItems(document, position) {
      const line = document.lineAt(position.line).text;
      const beforeCursor = line.slice(0, position.character);
      if (beforeCursor.trim() === '!') {
        const item = new vscode.CompletionItem('TXL application starter', vscode.CompletionItemKind.Snippet);
        item.detail = 'Insert window, styles, actions, and a starter app'; item.filterText = '!'; item.sortText = '0_starter';
        item.range = new vscode.Range(position.line, Math.max(0, position.character - 1), position.line, position.character);
        item.insertText = new vscode.SnippetString(starter);
        return [item];
      }
      if (/\btext\.$/i.test(beforeCursor)) {
        const range = new vscode.Range(position.line, position.character - 5, position.line, position.character);
        return makeItems([['text.headline', 'text.headline = ${1:Headline}', 'Large headline text']], range);
      }
      const media = beforeCursor.match(/^\s*(image|video)\s*=\s*(.*)$/i);
      if (media) return mediaItems(document, position, beforeCursor, media[1].toLowerCase());
      if (/^\s*(?:text(?:\([^)]+\)|\.[\w-]+)?|button|image|video|input|checkbox|list|line|space)\s*=/.test(beforeCursor)) return [];

      const propertyValue = beforeCursor.match(/^\s*(layout|color|background|font|size|weight|width|height|position|mode)\s*(?:=|:)\s*(.*)$/i);
      if (propertyValue) {
        const property = propertyValue[1].toLowerCase();
        const start = beforeCursor.search(/(?:=|:)\s*/) + beforeCursor.match(/(?:=|:)\s*/)[0].length;
        const range = new vscode.Range(position.line, start, position.line, position.character);
        if (property === 'font') {
          const partial = propertyValue[2].trim().toLowerCase();
          return googleFonts.filter(font => !partial || font.toLowerCase().startsWith(partial)).map(font => {
            const item = new vscode.CompletionItem(font, vscode.CompletionItemKind.Value);
            item.detail = 'Font family name (must be installed on the target Windows PC)'; item.range = range; item.insertText = font; return item;
          });
        }
        const values = property === 'mode' ? ['normal', 'maximized', 'fullscreen', 'borderless'] : styleValues[property] || [];
        const partial = propertyValue[2].trim().toLowerCase();
        return values.filter(value => !partial || value.toLowerCase().startsWith(partial)).map(value => {
          const item = new vscode.CompletionItem(value, vscode.CompletionItemKind.Value);
          item.detail = `Value for ${property}`; item.range = range; item.insertText = value; return item;
        });
      }

      const inWindow = isInsideBlock(document, position.line, 'window') || isInsideBlock(document, position.line, 'windows');
      if (inWindow) {
        const range = document.getWordRangeAtPosition(position);
        const prefix = range ? document.getText(range).toLowerCase() : '';
        return makeItems(windowProperties.filter(([label]) => !prefix || label.toLowerCase().startsWith(prefix)), range);
      }
      const inStyle = isInsideBlock(document, position.line, 'style');
      const wordRange = document.getWordRangeAtPosition(position);
      const prefix = wordRange ? document.getText(wordRange).toLowerCase() : '';
      const rows = inStyle ? [...styleProperties, ...sourceClasses(document)] : elementCompletions;
      return makeItems(rows.filter(([label]) => !prefix || label.toLowerCase().startsWith(prefix)), wordRange);
    }
  }, '!', '.'));

  context.subscriptions.push(vscode.languages.registerHoverProvider(selector, {
    provideHover(document, position) {
      const range = document.getWordRangeAtPosition(position);
      if (!range) return undefined;
      const word = document.getText(range);
      const help = {
        group: 'A group holds controls. `layout = vertical` stacks them top-to-bottom; `horizontal` places them side-by-side; `absolute` uses fixed Canvas coordinates.',
        style: 'Opens style definitions. A section uses `main:`; close the style block with `)`. Style properties use `name = value`.',
        text: 'Creates a text label. Use `text.headline = ...` for a large heading. Text after `text =` is content, not a TXL command position.',
        video: 'Plays a local video or HTTP(S) URL. TXL emits a WPF MediaElement; supported formats depend on Windows codecs.',
        button: 'Creates a native WPF button.',
        layout: '`vertical` stacks controls top-to-bottom; `horizontal` places them side-by-side; `absolute` allows `position = x18 y3`.'
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
