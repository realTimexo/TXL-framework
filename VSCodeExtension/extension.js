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
  ['text.headline', 'text.headline = ${1:Headline}', 'Add a large headline (alias of headline1)'],
  ['text.headline1', 'text.headline1 = ${1:Heading}', 'Add a level 1 heading'],
  ['text.headline2', 'text.headline2 = ${1:Heading}', 'Add a level 2 heading'],
  ['text.headline3', 'text.headline3 = ${1:Heading}', 'Add a level 3 heading'],
  ['text.headline4', 'text.headline4 = ${1:Heading}', 'Add a level 4 heading'],
  ['text.headline5', 'text.headline5 = ${1:Heading}', 'Add a level 5 heading'],
  ['button', 'button = ${1:Label}', 'Add a button'],
  ['image', 'image = ${1:assets/image.png}', 'Show a local image or HTTP(S) URL'],
  ['html', 'html = ${1:assets/page.html}', 'Embed a trusted local HTML page or HTTP(S) URL'],
  ['embed', 'embed = \"${1:<h2>Embedded HTML</h2>}\"', 'Embed inline HTML (alias of html)'],
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
  ['opacity', 'opacity = ${1:1}', 'Set opacity'],
  ['align', 'align = ${1|left,center,right,stretch|}', 'Align a text, control, or group'],
  ['radius', 'radius = ${1:12}', 'Round button corners or framed elements'],
  ['border', 'border = ${1:1 #333333}', 'Draw a border stroke'],
  ['shadow', 'shadow = ${1:4 12 #00000066}', 'Add a drop shadow'],
  ['layout', 'layout = ${1|vertical,horizontal,absolute|}', 'Choose group layout'],
  ['cursor', 'cursor = ${1|pointer,arrow,text,wait,cross,help,no,sizeall,sizenesw,sizens,sizewe,up|}', 'Choose a supported pointer cursor']
];

const windowProperties = [
  ['title', 'title: ${1:My app}', 'Set the title bar label'],
  ['icon', 'icon: ${1:assets/app.ico}', 'Project-relative window icon path'],
  ['size', 'size: ${1:1100x760}', 'Set window size as WIDTHxHEIGHT'],
  ['mode', 'mode: ${1|normal,maximized,fullscreen,borderless|}', 'Select the window mode']
];

const googleFonts = ['Lilita One','Roboto','Open Sans','Lato','Montserrat','Poppins','Inter','Oswald','Raleway','Nunito','Roboto Condensed','Playfair Display','Merriweather','Ubuntu','Rubik','Work Sans','DM Sans','Source Sans 3','PT Sans','Noto Sans','Fira Sans','Manrope','Quicksand','Mulish','Barlow','Karla','Cabin','Libre Franklin','Josefin Sans','Space Grotesk','Outfit','Plus Jakarta Sans','IBM Plex Sans','Inconsolata','Roboto Mono','Fira Code','Source Code Pro','JetBrains Mono','Space Mono','Bebas Neue','Comfortaa','Dancing Script','Pacifico','Lobster','Abril Fatface','Archivo','Arimo','Asap','Barlow Condensed','Bitter','Cairo','Crimson Text','DM Serif Display','Heebo','Jost','Kanit','Lexend','Libre Baskerville','Lora','Maven Pro','Mukta','Nanum Gothic','Newsreader','Oxygen','Prompt','Sora','Titillium Web','Urbanist','Varela Round','Yanone Kaffeesatz'];
const styleValues = {
  layout: ['vertical', 'horizontal', 'absolute'],
  align: ['left', 'center', 'right', 'stretch'],
  radius: ['4', '8', '12', '16'],
  border: ['1 #333333', '2 #FFFFFF', '1 red'],
  shadow: ['4 12 #00000066', 'none'],
  color: ['#20202A', '#F4F7FB', '#000000', '#FFFFFF', 'black', 'white', 'red', 'blue', 'green', 'transparent'],
  background: ['#20202A', '#F4F7FB', '#000000', '#FFFFFF', 'black', 'white', 'transparent'],
  size: ['12', '14', '16', '20', '24', '32', '48'],
  width: ['120', '180', '240', '320', '480'],
  height: ['120', '180', '240', '320'],
  weight: ['normal', 'bold'],
  position: ['x18 y3', 'x0 y0', 'x100 y100'],
  cursor: ['pointer', 'hand', 'arrow', 'appstarting', 'cross', 'help', 'text', 'no', 'sizeall', 'sizenesw', 'sizens', 'sizenwse', 'sizewe', 'up', 'wait', 'none', 'pen', 'scrollall', 'scrolln', 'scrollne', 'scrollns', 'scrollnw', 'scrolls', 'scrollse', 'scrollsw', 'scrollw', 'scrollwe']
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

function activeActionEvent(document, lineNumber) {
  let inActions = false, kind = null, headerIndent = '';
  for (let i = 0; i <= lineNumber; i++) {
    const line = document.lineAt(i).text;
    const trimmed = line.trim();
    if (/^actions\s*\(/i.test(trimmed)) { inActions = true; kind = null; continue; }
    if (!inActions) continue;
    if (trimmed === ')') { inActions = false; kind = null; continue; }
    const event = line.match(/^\s*[A-Za-z][\w-]*\((click|hover|keydown)\):\s*$/i);
    if (event) { kind = event[1].toLowerCase(); headerIndent = line.match(/^\s*/)[0]; }
  }
  return inActions && kind ? { kind, headerIndent } : null;
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
  const element = /^\s*(text|button|image|video|html|embed|input|checkbox|list|line|space)(?:\.[A-Za-z][\w-]*)?\(([A-Za-z][\w-]*)\)/i;
  const action = /^\s*([A-Za-z][\w-]*)\((click|hover|keydown)\):\s*$/i;
  for (let i = 0; i < document.lineCount; i++) {
    const line = document.lineAt(i).text;
    const elementMatch = line.match(element);
    const actionMatch = line.match(action);
    if (!elementMatch && !actionMatch) continue;
    const name = elementMatch ? elementMatch[2] : actionMatch[1];
    if (!classes.has(name.toLowerCase())) {
      const body = elementMatch && elementMatch[1].toLowerCase() === 'image'
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
    const filters = type === 'video' ? '**/*.{mp4,m4v,mov,wmv,avi,webm}' : type === 'html' ? '**/*.html' : '**/*.{png,jpg,jpeg,gif,bmp,webp,ico}';
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
  remote.insertText = new vscode.SnippetString('https://${1:example.com/media.' + (type === 'video' ? 'mp4' : type === 'html' ? 'html' : 'png') + '}');
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
        const headings = [['text.headline', 'text.headline = ${1:Headline}', 'Large headline text'], ['text.headline1', 'text.headline1 = ${1:Heading}', 'Level 1 heading'], ['text.headline2', 'text.headline2 = ${1:Heading}', 'Level 2 heading'], ['text.headline3', 'text.headline3 = ${1:Heading}', 'Level 3 heading'], ['text.headline4', 'text.headline4 = ${1:Heading}', 'Level 4 heading'], ['text.headline5', 'text.headline5 = ${1:Heading}', 'Level 5 heading']];
        const classes = sourceClasses(document).map(([name]) => { const className = name.replace(/:$/, ''); return [`text.${className}`, `text.${className} = \${1:Text}`, `Create text using class '${className}'`]; });
        return makeItems([...headings, ...classes], range);
      }
      const actionEvent = activeActionEvent(document, position.line);
      if (actionEvent) {
        const eventHeader = /^\s*[A-Za-z][\w-]*\((?:click|hover|keydown)\):\s*$/i.test(line);
        const indent = eventHeader ? `\n${actionEvent.headerIndent}  ` : '';
        const actionRows = actionEvent.kind === 'hover' ? styleProperties : [
          ['open', 'open = ${1:https://example.com}', 'Open a safe HTTP(S) or mailto link'],
          ['alert', 'alert = ${1:Message}', 'Show a Windows alert box'],
          ['success', 'success = ${1:Saved}', 'Show a success message'],
          ['confirm', 'confirm = ${1:Continue?}', 'Ask for confirmation'],
          ['prompt', 'prompt = ${1:state,title}', 'Ask for text and save it to state'],
          ['set', 'set = ${1:state,value}', 'Change persisted app state'],
          ['append', 'append = ${1:state,value}', 'Append text to a state value'],
          ['toggle', 'toggle = ${1:state}', 'Toggle a boolean state value'],
          ['clear', 'clear = ${1:state}', 'Clear a state value'],
          ['math', 'math = ${1:add,left,right,result}', 'Calculate and store a result'],
          ['listAdd', 'listAdd = ${1:tasks,input}', 'Add an item to a persistent task list'],
          ['arrayAdd', 'arrayAdd = ${1:items},${2:value}', 'Append a JSON-compatible value to an array state'],
          ['arrayGet', 'arrayGet = ${1:items},${2:0},${3:result}', 'Read an array item into another state'],
          ['arrayRemove', 'arrayRemove = ${1:items},${2:0}', 'Remove an item by zero-based array index'],
          ['objectSet', 'objectSet = ${1:profile},${2:name},${3:value}', 'Set a property on an object state'],
          ['objectGet', 'objectGet = ${1:profile},${2:name},${3:result}', 'Read an object property into another state'],
          ['objectRemove', 'objectRemove = ${1:profile},${2:name}', 'Remove an object property'],
          ['fileWrite', 'fileWrite = ${1:notes.txt},${2:Text to save}', 'Create or replace a file under the app data folder'],
          ['fileAppend', 'fileAppend = ${1:notes.txt},${2:More text}', 'Append text to a file under the app data folder'],
          ['fileRead', 'fileRead = ${1:notes.txt},${2:resultState}', 'Read an app data file into state'],
          ['fileDelete', 'fileDelete = ${1:notes.txt}', 'Delete a file under the app data folder'],
          ['httpGet', 'httpGet = ${1:https://api.example.com/data},${2:responseState}', 'GET JSON/text asynchronously (30-second timeout)'],
          ['httpPost', 'httpPost = ${1:https://api.example.com/data},${2:jsonState},${3:responseState}', 'POST a JSON body asynchronously (30-second timeout)'],
          ['timerStart', 'timerStart = ${1:clock},${2:1000},${3:seconds},${4:increment}', 'Start a repeating state timer (fixed value, increment, or toggle)'],
          ['timerStop', 'timerStop = ${1:clock}', 'Stop a named repeating timer'],
          ['dbExecute', 'dbExecute = ${1:app.db},${2:CREATE TABLE IF NOT EXISTS items (id INTEGER PRIMARY KEY, name TEXT)}', 'Execute SQLite DDL or an INSERT/UPDATE/DELETE statement'],
          ['dbQuery', 'dbQuery = ${1:app.db},${2:SELECT * FROM items},${3:rows}', 'Store SQLite rows as a JSON array in state'],
          ['dbScalar', 'dbScalar = ${1:app.db},${2:SELECT COUNT(*) FROM items},${3:count}', 'Store a SQLite scalar result in state'],
          ['dbBegin', 'dbBegin = ${1:app.db}', 'Begin a SQLite transaction; commit or rollback it explicitly'],
          ['dbCommit', 'dbCommit = ${1:app.db}', 'Commit and close the active SQLite transaction'],
          ['dbRollback', 'dbRollback = ${1:app.db}', 'Roll back and close the active SQLite transaction'],
          ['try', 'try(${1:httpGet(https://api.example.com/data,response)},${2:errorState})', 'Await one action and store any caught error message in state'],
          ['throw', 'throw(${1:Something went wrong})', 'Raise an error for a surrounding try action or page catch'],
          ['page', 'page = ${1:pages/next.txl}', 'Navigate to a compiled TXL page'],
          ['key', 'key = ${1|Enter,Space,Escape,Tab|}', 'Filter a keydown action']
        ];
        const wordRange = document.getWordRangeAtPosition(position);
        const prefix = wordRange ? document.getText(wordRange).toLowerCase() : '';
        return makeItems(actionRows.filter(([label]) => !prefix || label.toLowerCase().startsWith(prefix)).map(([label, body, detail]) => [label, indent + body, detail]), wordRange);
      }
      const media = beforeCursor.match(/^\s*(image|video|html)\s*=\s*(.*)$/i);
      if (media) return mediaItems(document, position, beforeCursor, media[1].toLowerCase());
      if (/^\s*(?:text(?:\([^)]+\)|\.[\w-]+)?|button|image|video|html|embed|input|checkbox|list|line|space)\s*=/.test(beforeCursor)) return [];

      const propertyValue = beforeCursor.match(/^\s*(layout|align|color|background|font|size|weight|width|height|position|radius|border|stroke|shadow|mode|cursor)\s*(?:=|:)\s*(.*)$/i);
      if (propertyValue) {
        const property = propertyValue[1].toLowerCase();
        const start = beforeCursor.search(/(?:=|:)\s*/) + beforeCursor.match(/(?:=|:)\s*/)[0].length;
        const range = new vscode.Range(position.line, start, position.line, position.character);
        if (property === 'font') {
          const partial = propertyValue[2].trim().toLowerCase();
          const fontItems = googleFonts.filter(font => !partial || font.toLowerCase().startsWith(partial)).map(font => {
            const item = new vscode.CompletionItem(font, vscode.CompletionItemKind.Value);
            item.detail = 'Installed family name; alternatively use a direct HTTPS .ttf/.otf URL to package it'; item.range = range; item.insertText = font; return item;
          });
          if (!partial || partial.startsWith('https://') || 'https://'.startsWith(partial)) { const remote = new vscode.CompletionItem('https:// downloadable .ttf/.otf font URL', vscode.CompletionItemKind.Value); remote.detail = 'Build downloads and packages direct HTTPS TTF/OTF fonts'; remote.range = range; remote.insertText = new vscode.SnippetString('https://${1:fonts.example.com/font.ttf}'); fontItems.push(remote); }
          return fontItems;
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
  }, '!', '.', ':', ')'));

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
