// ----------------------------------------------------------------------
// Supabase configuration – shared game storage in the cloud
// ----------------------------------------------------------------------
// Assumes supabaseClient is already initialized (see index.html)
// Local user preferences are still stored in a small IndexedDB

const USER_PREFS_DB = 'FunHtmlGamesUserPrefs';
const USER_PREFS_VERSION = 1;
let prefsDb = null;

function openPrefsDB() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(USER_PREFS_DB, USER_PREFS_VERSION);
        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            prefsDb = request.result;
            resolve(prefsDb);
        };
        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains('userPrefs')) {
                // Each game's user-specific data: isInstalled, windowStyles, manifest (if overridden)
                db.createObjectStore('userPrefs', { keyPath: 'gameId' });
            }
        };
    });
}

function promisifyRequest(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

window.gameStore = {
    // ------------------------------------------------------------------
    // Shared game data – Supabase (visible to everyone)
    // ------------------------------------------------------------------
    saveGame: async (gameId, gameName, filePaths, fileContents) => {
        // 1. Upload each file to Supabase Storage bucket 'games'
        const uploadPromises = filePaths.map(async (path, index) => {
            const content = fileContents[index];
            const blob = new Blob([content]);
            const fileName = `${gameId}/${path}`;
            const { error } = await supabaseClient.storage
                .from('games')
                .upload(fileName, blob, { upsert: true });
            if (error) throw error;
        });
        await Promise.all(uploadPromises);

        // 2. Insert or update game metadata in 'games' table
        const { error } = await supabaseClient
            .from('games')
            .upsert({
                id: gameId,
                name: gameName,
                created_at: new Date().toISOString(),
                // We'll store filePaths as a JSON array for reference
                files: filePaths
            });
        if (error) throw error;
    },

    getAllGames: async () => {
        // Fetch all games from Supabase
        const { data, error } = await supabaseClient
            .from('games')
            .select('*')
            .order('created_at', { ascending: false });
        if (error) throw error;

        // Merge with local user preferences (isInstalled, windowStyles, manifest)
        await openPrefsDB();
        const tx = prefsDb.transaction('userPrefs', 'readonly');
        const store = tx.objectStore('userPrefs');
        const prefs = await promisifyRequest(store.getAll());

        return data.map(game => {
            const userPref = prefs.find(p => p.gameId === game.id) || {};
            return {
                id: game.id,
                name: game.name,
                isInstalled: userPref.isInstalled || false,
                windowStyles: userPref.windowStyles || null,
                manifest: userPref.manifest || null,
                files: game.files || []
            };
        });
    },

    getGame: async (gameId) => {
        // Fetch game metadata from Supabase
        const { data, error } = await supabaseClient
            .from('games')
            .select('*')
            .eq('id', gameId)
            .single();
        if (error) throw error;
        if (!data) return null;

        // Merge with local user preferences
        await openPrefsDB();
        const tx = prefsDb.transaction('userPrefs', 'readonly');
        const store = tx.objectStore('userPrefs');
        const userPref = await promisifyRequest(store.get(gameId)) || {};

        return {
            id: data.id,
            name: data.name,
            isInstalled: userPref.isInstalled || false,
            windowStyles: userPref.windowStyles || null,
            manifest: userPref.manifest || null,
            files: data.files || []
        };
    },

    getGameFile: async (gameId, filePath) => {
        // Download file from Supabase Storage
        const fileName = `${gameId}/${filePath}`;
        const { data, error } = await supabaseClient.storage
            .from('games')
            .download(fileName);
        if (error) throw error;
        if (!data) return null;
        const buffer = await data.arrayBuffer();
        return new Uint8Array(buffer);
    },

    saveGameFile: async (gameId, filePath, content) => {
        // Upload a file (used by filecopier)
        const blob = new Blob([content]);
        const fileName = `${gameId}/${filePath}`;
        const { error } = await supabaseClient.storage
            .from('games')
            .upload(fileName, blob, { upsert: true });
        if (error) throw error;

        // Also update the files list in the metadata
        const { data: game, error: fetchError } = await supabaseClient
            .from('games')
            .select('files')
            .eq('id', gameId)
            .single();
        if (fetchError) throw fetchError;
        const files = game.files || [];
        if (!files.includes(filePath)) {
            files.push(filePath);
            const { error: updateError } = await supabaseClient
                .from('games')
                .update({ files })
                .eq('id', gameId);
            if (updateError) throw updateError;
        }
    },

    // ------------------------------------------------------------------
    // User‑specific preferences – stored locally in IndexedDB
    // ------------------------------------------------------------------
    setInstalled: async (gameId, installed) => {
        await openPrefsDB();
        const tx = prefsDb.transaction('userPrefs', 'readwrite');
        const store = tx.objectStore('userPrefs');
        let prefs = await promisifyRequest(store.get(gameId));
        if (!prefs) prefs = { gameId };
        prefs.isInstalled = installed;
        await promisifyRequest(store.put(prefs));
    },

    setWindowStyles: async (gameId, styles) => {
        await openPrefsDB();
        const tx = prefsDb.transaction('userPrefs', 'readwrite');
        const store = tx.objectStore('userPrefs');
        let prefs = await promisifyRequest(store.get(gameId));
        if (!prefs) prefs = { gameId };
        prefs.windowStyles = styles;
        await promisifyRequest(store.put(prefs));
    },

    setManifest: async (gameId, manifest) => {
        await openPrefsDB();
        const tx = prefsDb.transaction('userPrefs', 'readwrite');
        const store = tx.objectStore('userPrefs');
        let prefs = await promisifyRequest(store.get(gameId));
        if (!prefs) prefs = { gameId };
        prefs.manifest = manifest;
        await promisifyRequest(store.put(prefs));
    },

    getManifest: async (gameId) => {
        await openPrefsDB();
        const tx = prefsDb.transaction('userPrefs', 'readonly');
        const store = tx.objectStore('userPrefs');
        const prefs = await promisifyRequest(store.get(gameId));
        return prefs ? prefs.manifest : null;
    }
};

// ----------------------------------------------------------------------
// Directory upload handler – unchanged (still uses File System Access API)
// ----------------------------------------------------------------------
window.uploadGameFolder = async function (dotNetHelper) {
    try {
        const directoryHandle = await window.showDirectoryPicker();
        const files = [];
        const filePaths = [];
        const fileContents = [];

        async function readDirectory(dirHandle, basePath = '') {
            for await (const entry of dirHandle.values()) {
                const path = basePath ? `${basePath}/${entry.name}` : entry.name;
                if (entry.kind === 'file') {
                    const file = await entry.getFile();
                    const buffer = await file.arrayBuffer();
                    files.push({ path, buffer });
                } else if (entry.kind === 'directory') {
                    await readDirectory(entry, path);
                }
            }
        }

        await readDirectory(directoryHandle);

        for (const f of files) {
            filePaths.push(f.path);
            fileContents.push(new Uint8Array(f.buffer));
        }

        const gameId = `game_${Date.now()}`;
        const gameName = directoryHandle.name;

        await dotNetHelper.invokeMethodAsync('OnGameFolderUploaded', gameId, gameName, filePaths, fileContents);
    } catch (err) {
        console.error('Upload failed', err);
    }
};