const fs = require('fs');
const path = require('path');

// Definition of entities
// Group:
// 1. Auth & Admin (top left)
// 2. Lobby & Session Hub (top center / center)
// 3. Characters & Net (top right)
// 4. Session Runtime Progress (mid)
// 5. Game Master Data (bottom)

const entities = [
  // 1. Auth & Admin
  {
    id: 'Account',
    title: 'Account',
    x: 60, y: 160, w: 180, h: 140,
    headerColor: '#2563eb',
    fields: [
      { name: 'id', type: 'string', isPk: true },
      { name: 'username', type: 'string' },
      { name: 'email', type: 'string' },
      { name: 'role', type: 'enum' },
      { name: 'is_banned', type: 'bool' },
      { name: 'created_at', type: 'datetime' }
    ]
  },
  {
    id: 'Admin',
    title: 'Admin',
    x: 290, y: 80, w: 170, h: 100,
    headerColor: '#1e40af',
    fields: [
      { name: 'admin_id', type: 'string', isPk: true },
      { name: 'account_id', type: 'string', isFk: true },
      { name: 'permissions', type: 'string' }
    ]
  },
  {
    id: 'Mod_Action_log',
    title: 'Mod_Action_log',
    x: 510, y: 80, w: 190, h: 150,
    headerColor: '#dc2626',
    fields: [
      { name: 'log_id', type: 'string', isPk: true },
      { name: 'admin_id', type: 'string', isFk: true },
      { name: 'target_type', type: 'string' },
      { name: 'target_id', type: 'string' },
      { name: 'session_id', type: 'string', isFk: true, isNew: true },
      { name: 'lobby_code', type: 'string', isFk: true, isNew: true },
      { name: 'action', type: 'string' }
    ]
  },
  {
    id: 'Player_Profile',
    title: 'Player_Profile',
    x: 290, y: 240, w: 180, h: 130,
    headerColor: '#0284c7',
    fields: [
      { name: 'player_id', type: 'string', isPk: true },
      { name: 'account_id', type: 'string', isFk: true },
      { name: 'display_name', type: 'string' },
      { name: 'avatar_url', type: 'string' },
      { name: 'total_play_time', type: 'int' }
    ]
  },
  {
    id: 'Progression',
    title: 'Progression',
    x: 60, y: 440, w: 180, h: 110,
    headerColor: '#0d9488',
    fields: [
      { name: 'progression_id', type: 'string', isPk: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'highest_chapter', type: 'int' },
      { name: 'total_stars', type: 'int' }
    ]
  },

  // 2. Lobby & Session Core
  {
    id: 'Lobby',
    title: 'Lobby',
    x: 770, y: 160, w: 180, h: 130,
    headerColor: '#9333ea',
    fields: [
      { name: 'lobby_code', type: 'string', isPk: true },
      { name: 'host_player_id', type: 'string', isFk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'status', type: 'enum' },
      { name: 'created_at', type: 'datetime' }
    ]
  },
  {
    id: 'Lobby_Member',
    title: 'Lobby_Member',
    x: 540, y: 270, w: 180, h: 130,
    headerColor: '#7c3aed',
    fields: [
      { name: 'member_id', type: 'string', isPk: true },
      { name: 'lobby_code', type: 'string', isFk: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'role_pref', type: 'enum' },
      { name: 'is_ready', type: 'bool' }
    ]
  },
  {
    id: 'Game_Session',
    title: 'Game_Session ★ CENTRAL HUB',
    x: 1040, y: 180, w: 230, h: 210,
    headerColor: '#d97706', // Gold/Amber highlight
    isHub: true,
    fields: [
      { name: 'session_id', type: 'string', isPk: true },
      { name: 'lobby_code', type: 'string', isFk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'current_checkpoint_id', type: 'string', isFk: true, isNew: true },
      { name: 'current_boss_phase_id', type: 'string', isFk: true, isNew: true },
      { name: 'status', type: 'enum' },
      { name: 'relay_join_code', type: 'string' },
      { name: 'duration_seconds', type: 'int' },
      { name: 'death_count', type: 'int' },
      { name: 'started_at', type: 'datetime' }
    ]
  },
  {
    id: 'Session_Player',
    title: 'Session_Player',
    x: 1340, y: 250, w: 190, h: 160,
    headerColor: '#ea580c',
    fields: [
      { name: 'session_player_id', type: 'string', isPk: true },
      { name: 'session_id', type: 'string', isFk: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'character_id', type: 'string', isFk: true },
      { name: 'role', type: 'enum(Dad/Child)' },
      { name: 'is_host', type: 'bool' },
      { name: 'ping_ms', type: 'int' }
    ]
  },
  {
    id: 'Network_State_log',
    title: 'Network_State_log',
    x: 1340, y: 70, w: 190, h: 130,
    headerColor: '#b91c1c',
    fields: [
      { name: 'log_id', type: 'string', isPk: true },
      { name: 'session_id', type: 'string', isFk: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'rtt_ms', type: 'float' },
      { name: 'packet_loss', type: 'float' }
    ]
  },

  // 3. Characters & Stats
  {
    id: 'Character',
    title: 'Character (Dad / Child)',
    x: 1600, y: 250, w: 190, h: 120,
    headerColor: '#059669',
    fields: [
      { name: 'character_id', type: 'string', isPk: true },
      { name: 'name', type: 'string' },
      { name: 'physics_type', type: 'string' },
      { name: 'primary_stat', type: 'string' }
    ]
  },
  {
    id: 'Character_Attribute',
    title: 'Character_Attribute',
    x: 1840, y: 250, w: 180, h: 130,
    headerColor: '#10b981',
    fields: [
      { name: 'attribute_id', type: 'string', isPk: true },
      { name: 'character_id', type: 'string', isFk: true },
      { name: 'stat_name', type: 'string' },
      { name: 'starting_value', type: 'float' },
      { name: 'regen_rate', type: 'float' }
    ]
  },
  {
    id: 'Combat_Ability',
    title: 'Combat_Ability',
    x: 1840, y: 110, w: 180, h: 120,
    headerColor: '#10b981',
    fields: [
      { name: 'ability_id', type: 'string', isPk: true },
      { name: 'character_id', type: 'string', isFk: true },
      { name: 'ability_name', type: 'string' },
      { name: 'cooldown', type: 'float' }
    ]
  },

  // 4. Runtime Progress Junctions (Tying Player_Profile AND Game_Session)
  {
    id: 'Player_Achievement',
    title: 'Player_Achievement',
    x: 270, y: 440, w: 180, h: 110,
    headerColor: '#4f46e5',
    fields: [
      { name: 'id', type: 'string', isPk: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'achievement_id', type: 'string', isFk: true },
      { name: 'unlocked_at', type: 'datetime' }
    ]
  },
  {
    id: 'Player_Tool_Unlock',
    title: 'Player_Tool_Unlock',
    x: 480, y: 440, w: 180, h: 110,
    headerColor: '#4f46e5',
    fields: [
      { name: 'id', type: 'string', isPk: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'tool_id', type: 'string', isFk: true },
      { name: 'unlocked_at', type: 'datetime' }
    ]
  },
  {
    id: 'Player_Objective_Progress',
    title: 'Player_Objective_Progress',
    x: 690, y: 440, w: 200, h: 140,
    headerColor: '#ea580c',
    fields: [
      { name: 'id', type: 'string', isPk: true },
      { name: 'session_id', type: 'string', isFk: true, isNew: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'puzzle_trigger_id', type: 'string', isFk: true },
      { name: 'is_completed', type: 'bool' },
      { name: 'triggered_at', type: 'datetime' }
    ]
  },
  {
    id: 'Player_Collectible',
    title: 'Player_Collectible',
    x: 920, y: 440, w: 180, h: 130,
    headerColor: '#ea580c',
    fields: [
      { name: 'id', type: 'string', isPk: true },
      { name: 'session_id', type: 'string', isFk: true, isNew: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'collectible_id', type: 'string', isFk: true },
      { name: 'collected_at', type: 'datetime' }
    ]
  },
  {
    id: 'Minigame_Score',
    title: 'Minigame_Score',
    x: 1130, y: 440, w: 180, h: 140,
    headerColor: '#ea580c',
    fields: [
      { name: 'score_id', type: 'string', isPk: true },
      { name: 'session_id', type: 'string', isFk: true, isNew: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'minigame_id', type: 'string', isFk: true },
      { name: 'score', type: 'int' },
      { name: 'completion_time', type: 'float' }
    ]
  },
  {
    id: 'Player_Quest_Progress',
    title: 'Player_Quest_Progress',
    x: 1340, y: 440, w: 190, h: 140,
    headerColor: '#ea580c',
    fields: [
      { name: 'id', type: 'string', isPk: true },
      { name: 'session_id', type: 'string', isFk: true, isNew: true },
      { name: 'player_id', type: 'string', isFk: true },
      { name: 'quest_object_id', type: 'string', isFk: true },
      { name: 'status', type: 'enum' },
      { name: 'completed_at', type: 'datetime' }
    ]
  },

  // 5. Master Data & Chapter Sub-Elements
  {
    id: 'Achievement',
    title: 'Achievement',
    x: 270, y: 640, w: 170, h: 110,
    headerColor: '#334155',
    fields: [
      { name: 'achievement_id', type: 'string', isPk: true },
      { name: 'title', type: 'string' },
      { name: 'description', type: 'string' },
      { name: 'points', type: 'int' }
    ]
  },
  {
    id: 'Tool',
    title: 'Tool',
    x: 480, y: 640, w: 170, h: 110,
    headerColor: '#334155',
    fields: [
      { name: 'tool_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'name', type: 'string' },
      { name: 'tool_type', type: 'string' }
    ]
  },
  {
    id: 'Puzzle_Trigger',
    title: 'Puzzle_Trigger',
    x: 690, y: 640, w: 180, h: 110,
    headerColor: '#334155',
    fields: [
      { name: 'trigger_id', type: 'string', isPk: true },
      { name: 'puzzle_id', type: 'string', isFk: true },
      { name: 'name', type: 'string' },
      { name: 'sequence', type: 'int' }
    ]
  },
  {
    id: 'Puzzle_Element',
    title: 'Puzzle_Element',
    x: 690, y: 780, w: 180, h: 120,
    headerColor: '#1e293b',
    fields: [
      { name: 'puzzle_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'puzzle_type', type: 'string' },
      { name: 'required_role', type: 'enum' }
    ]
  },
  {
    id: 'Boss_Phase',
    title: 'Boss_Phase',
    x: 900, y: 640, w: 180, h: 120,
    headerColor: '#334155',
    fields: [
      { name: 'phase_id', type: 'string', isPk: true },
      { name: 'boss_id', type: 'string', isFk: true },
      { name: 'phase_num', type: 'int' },
      { name: 'hp_threshold', type: 'float' }
    ]
  },
  {
    id: 'Boss',
    title: 'Boss',
    x: 900, y: 780, w: 180, h: 110,
    headerColor: '#1e293b',
    fields: [
      { name: 'boss_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'name', type: 'string' },
      { name: 'phases_count', type: 'int' }
    ]
  },
  {
    id: 'Collectible',
    title: 'Collectible',
    x: 1110, y: 640, w: 170, h: 110,
    headerColor: '#334155',
    fields: [
      { name: 'collectible_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'name', type: 'string' },
      { name: 'rarity', type: 'string' }
    ]
  },
  {
    id: 'Minigame',
    title: 'Minigame',
    x: 1310, y: 640, w: 170, h: 110,
    headerColor: '#334155',
    fields: [
      { name: 'minigame_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'title', type: 'string' },
      { name: 'rules_type', type: 'string' }
    ]
  },
  {
    id: 'Checkpoint',
    title: 'Checkpoint',
    x: 1510, y: 640, w: 180, h: 120,
    headerColor: '#334155',
    fields: [
      { name: 'checkpoint_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'name', type: 'string' },
      { name: 'respawn_coord', type: 'Vector3' }
    ]
  },
  {
    id: 'CutScene',
    title: 'CutScene',
    x: 1720, y: 640, w: 170, h: 110,
    headerColor: '#334155',
    fields: [
      { name: 'cutscene_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'title', type: 'string' },
      { name: 'duration', type: 'float' }
    ]
  },
  {
    id: 'Quest_Object',
    title: 'Quest_Object',
    x: 1910, y: 440, w: 170, h: 110,
    headerColor: '#334155',
    fields: [
      { name: 'quest_object_id', type: 'string', isPk: true },
      { name: 'quest_id', type: 'string', isFk: true },
      { name: 'title', type: 'string' },
      { name: 'target_count', type: 'int' }
    ]
  },
  {
    id: 'Quest',
    title: 'Quest',
    x: 1910, y: 640, w: 180, h: 120,
    headerColor: '#1e293b',
    fields: [
      { name: 'quest_id', type: 'string', isPk: true },
      { name: 'chapter_id', type: 'string', isFk: true },
      { name: 'name', type: 'string' },
      { name: 'quest_type', type: 'string' }
    ]
  },
  {
    id: 'Chapter',
    title: 'Chapter (Master Level)',
    x: 1180, y: 920, w: 220, h: 140,
    headerColor: '#0f766e',
    fields: [
      { name: 'chapter_id', type: 'string', isPk: true },
      { name: 'name', type: 'string' },
      { name: 'theme', type: 'string' },
      { name: 'order_index', type: 'int' },
      { name: 'description', type: 'text' },
      { name: 'is_active', type: 'bool' }
    ]
  }
];

// Relationships / Edges
const edges = [
  // Auth & Admin
  { from: 'Account', to: 'Admin', label: 'is_admin', fromCard: '1', toCard: '0..1' },
  { from: 'Account', to: 'Player_Profile', label: 'has_profile', fromCard: '1', toCard: '1' },
  { from: 'Admin', to: 'Mod_Action_log', label: 'logs_action', fromCard: '1', toCard: '*' },

  // Lobby
  { from: 'Player_Profile', to: 'Lobby_Member', label: 'joins_as', fromCard: '1', toCard: '*' },
  { from: 'Lobby', to: 'Lobby_Member', label: 'contains', fromCard: '1', toCard: '1..*' },
  { from: 'Lobby', to: 'Game_Session', label: 'spawns_on_ready', fromCard: '1', toCard: '0..1', isCore: true },

  // GAME SESSION HUB CONNECTIONS (HIGHLIGHTED & EXPANDED)
  { from: 'Game_Session', to: 'Session_Player', label: 'has_players (Dad & Child)', fromCard: '1', toCard: '2', isCore: true },
  { from: 'Player_Profile', to: 'Session_Player', label: 'participates_as', fromCard: '1', toCard: '*' },
  { from: 'Character', to: 'Session_Player', label: 'assigned_character', fromCard: '1', toCard: '*' },
  { from: 'Game_Session', to: 'Network_State_log', label: 'monitors_network', fromCard: '1', toCard: '*', isCore: true },
  { from: 'Game_Session', to: 'Chapter', label: 'session_runs_in_chapter', fromCard: '*', toCard: '1', isCore: true },
  { from: 'Game_Session', to: 'Checkpoint', label: 'current_active_checkpoint', fromCard: '*', toCard: '0..1', isNew: true, isCore: true },
  { from: 'Game_Session', to: 'Boss_Phase', label: 'current_boss_phase', fromCard: '*', toCard: '0..1', isNew: true, isCore: true },

  // Connect Game_Session to Runtime Progress (THE CRITICAL NEW RELATIONSHIPS)
  { from: 'Game_Session', to: 'Player_Objective_Progress', label: 'session_puzzle_triggers', fromCard: '1', toCard: '*', isNew: true, isCore: true },
  { from: 'Game_Session', to: 'Player_Collectible', label: 'session_collected_items', fromCard: '1', toCard: '*', isNew: true, isCore: true },
  { from: 'Game_Session', to: 'Minigame_Score', label: 'session_minigame_scores', fromCard: '1', toCard: '*', isNew: true, isCore: true },
  { from: 'Game_Session', to: 'Player_Quest_Progress', label: 'session_quest_progress', fromCard: '1', toCard: '*', isNew: true, isCore: true },

  // Moderation links to Game_Session & Lobby
  { from: 'Mod_Action_log', to: 'Game_Session', label: 'aborts/inspects_session', fromCard: '*', toCard: '0..1', isNew: true },
  { from: 'Mod_Action_log', to: 'Lobby', label: 'closes_lobby (MOD-001)', fromCard: '*', toCard: '0..1', isNew: true },

  // Player Profile links to progress
  { from: 'Player_Profile', to: 'Progression', label: 'saves_progress', fromCard: '1', toCard: '1' },
  { from: 'Player_Profile', to: 'Player_Achievement', label: 'unlocked', fromCard: '1', toCard: '*' },
  { from: 'Player_Profile', to: 'Player_Tool_Unlock', label: 'unlocked', fromCard: '1', toCard: '*' },
  { from: 'Player_Profile', to: 'Player_Objective_Progress', label: 'profile_ref', fromCard: '1', toCard: '*' },
  { from: 'Player_Profile', to: 'Player_Collectible', label: 'collects', fromCard: '1', toCard: '*' },
  { from: 'Player_Profile', to: 'Minigame_Score', label: 'player_score', fromCard: '1', toCard: '*' },
  { from: 'Player_Profile', to: 'Player_Quest_Progress', label: 'player_progress', fromCard: '1', toCard: '*' },

  // Character stats
  { from: 'Character', to: 'Character_Attribute', label: 'has_stats', fromCard: '1', toCard: '*' },
  { from: 'Character', to: 'Combat_Ability', label: 'has_abilities', fromCard: '1', toCard: '*' },

  // Progress to Master elements
  { from: 'Achievement', to: 'Player_Achievement', label: 'unlocked_via', fromCard: '1', toCard: '*' },
  { from: 'Tool', to: 'Player_Tool_Unlock', label: 'unlocks_tool', fromCard: '1', toCard: '*' },
  { from: 'Puzzle_Trigger', to: 'Player_Objective_Progress', label: 'triggers_objective', fromCard: '1', toCard: '*' },
  { from: 'Collectible', to: 'Player_Collectible', label: 'collected_item', fromCard: '1', toCard: '*' },
  { from: 'Minigame', to: 'Minigame_Score', label: 'minigame_recorded', fromCard: '1', toCard: '*' },
  { from: 'Quest_Object', to: 'Player_Quest_Progress', label: 'progresses_object', fromCard: '1', toCard: '*' },

  // Master Content tree
  { from: 'Puzzle_Element', to: 'Puzzle_Trigger', label: 'contains_triggers', fromCard: '1', toCard: '*' },
  { from: 'Boss', to: 'Boss_Phase', label: 'has_phases', fromCard: '1', toCard: '*' },
  { from: 'Quest', to: 'Quest_Object', label: 'has_objectives', fromCard: '1', toCard: '*' },

  // Chapter relationships
  { from: 'Chapter', to: 'Tool', label: 'introduces', fromCard: '1', toCard: '*' },
  { from: 'Chapter', to: 'Puzzle_Element', label: 'contains', fromCard: '1', toCard: '*' },
  { from: 'Chapter', to: 'Boss', label: 'has_boss', fromCard: '1', toCard: '0..1' },
  { from: 'Chapter', to: 'Collectible', label: 'hides', fromCard: '1', toCard: '*' },
  { from: 'Chapter', to: 'Minigame', label: 'contains', fromCard: '1', toCard: '*' },
  { from: 'Chapter', to: 'Checkpoint', label: 'contains', fromCard: '1', toCard: '*' },
  { from: 'Chapter', to: 'CutScene', label: 'triggers', fromCard: '1', toCard: '*' },
  { from: 'Chapter', to: 'Quest', label: 'contains', fromCard: '1', toCard: '*' }
];

console.log(`Entities count: ${entities.length}, Edges count: ${edges.length}`);

// Generate Draw.io XML
function generateDrawioXml() {
  let cellId = 2;
  const entityCellMap = {};
  let xmlCells = '';

  // Grid and styles
  entities.forEach(ent => {
    const parentId = cellId++;
    entityCellMap[ent.id] = parentId;

    const strokeColor = ent.isHub ? '#f59e0b' : '#3f3f46';
    const headerBg = ent.headerColor;
    const bodyBg = ent.isHub ? '#291e0a' : '#18181b';
    const strokeWidth = ent.isHub ? '3' : '1';

    let fieldsHtml = ent.fields.map(f => {
      let badge = '';
      if (f.isPk) badge = '<font color="#fbbf24"><b>[PK]</b></font> ';
      else if (f.isFk && f.isNew) badge = '<font color="#f97316"><b>[FK*]</b></font> ';
      else if (f.isFk) badge = '<font color="#60a5fa"><b>[FK]</b></font> ';
      else if (f.isNew) badge = '<font color="#34d399"><b>[*]</b></font> ';
      return `+ ${badge}${f.name}: <font color="#94a3b8">${f.type}</font>`;
    }).join('<br/>');

    const value = `&lt;b&gt;${ent.title}&lt;/b&gt;&lt;hr/&gt;${fieldsHtml}`;

    xmlCells += `
        <mxCell id="${parentId}" value="${value}" style="swimlane;fontStyle=0;childLayout=stackLayout;horizontal=1;startSize=26;horizontalStack=0;resizeParent=1;resizeParentMax=0;resizeLast=0;collapsible=1;marginBottom=0;align=left;fontSize=11;fillColor=${bodyBg};strokeColor=${strokeColor};strokeWidth=${strokeWidth};fontColor=#f8fafc;" vertex="1" parent="1">
          <mxGeometry x="${ent.x}" y="${ent.y}" width="${ent.w}" height="${ent.h}" as="geometry" />
        </mxCell>`;
  });

  // Edges
  edges.forEach((edge, idx) => {
    const edgeId = cellId++;
    const srcId = entityCellMap[edge.from];
    const tgtId = entityCellMap[edge.to];
    if (!srcId || !tgtId) return;

    let strokeColor = '#71717a';
    let strokeWidth = '1';
    let dashed = '0';

    if (edge.isCore && edge.isNew) {
      strokeColor = '#f59e0b'; // Glowing Amber for NEW Game_Session links!
      strokeWidth = '2.5';
      dashed = '1';
    } else if (edge.isCore) {
      strokeColor = '#38bdf8'; // Sky Blue for existing Core session links
      strokeWidth = '2';
    } else if (edge.isNew) {
      strokeColor = '#ec4899'; // Pink for other new moderation links
      strokeWidth = '1.5';
      dashed = '1';
    }

    const label = edge.label ? `&lt;font color="${strokeColor}"&gt;${edge.label}&lt;/font&gt;` : '';

    xmlCells += `
        <mxCell id="${edgeId}" value="${label}" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;entryX=0.5;entryY=0;entryDx=0;entryDy=0;strokeColor=${strokeColor};strokeWidth=${strokeWidth};dashed=${dashed};fontColor=#94a3b8;fontSize=9;" edge="1" parent="1" source="${srcId}" target="${tgtId}">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>`;
  });

  const fullXml = `<?xml version="1.0" encoding="UTF-8"?>
<mxfile host="app.diagrams.net" modified="${new Date().toISOString()}" agent="Antigravity" version="21.0.0" type="device">
  <diagram name="Hold-My-Hand-ERD" id="hold-my-hand-erd-v2">
    <mxGraphModel dx="2200" dy="1400" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="2400" pageHeight="1400" background="#0f172a">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
${xmlCells}
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>`;

  return fullXml;
}

// Generate Standalone Interactive HTML
function generateHtmlViewer() {
  const jsonData = JSON.stringify({ entities, edges }, null, 2);

  return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Hold My Hand — Sơ đồ Thực Thể ERD Chuẩn Hoá (Tâm Điểm Game_Session)</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background: #090d16;
      color: #f1f5f9;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      overflow: hidden;
      width: 100vw;
      height: 100vh;
      display: flex;
      flex-direction: column;
    }
    header {
      padding: 12px 24px;
      background: rgba(15, 23, 42, 0.95);
      backdrop-filter: blur(8px);
      border-bottom: 1px solid #1e293b;
      display: flex;
      justify-content: space-between;
      align-items: center;
      z-index: 100;
    }
    .header-title h1 {
      font-size: 1.15rem;
      font-weight: 700;
      color: #38bdf8;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .header-title p {
      font-size: 0.8rem;
      color: #94a3b8;
    }
    .controls {
      display: flex;
      gap: 12px;
      align-items: center;
    }
    .btn {
      padding: 6px 14px;
      background: #1e293b;
      border: 1px solid #334155;
      color: #f8fafc;
      border-radius: 6px;
      font-size: 0.8rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn:hover { background: #334155; border-color: #38bdf8; }
    .btn-primary { background: #0284c7; border-color: #38bdf8; font-weight: 600; }
    .btn-primary:hover { background: #0369a1; }

    .legend {
      display: flex;
      gap: 16px;
      font-size: 0.75rem;
      background: #0f172a;
      padding: 6px 16px;
      border-radius: 20px;
      border: 1px solid #1e293b;
    }
    .legend-item { display: flex; align-items: center; gap: 6px; }
    .badge-dot { width: 10px; height: 10px; border-radius: 50%; }

    #viewport {
      flex: 1;
      position: relative;
      cursor: grab;
      overflow: hidden;
      background-image: 
        radial-gradient(circle at 1px 1px, rgba(255,255,255,0.06) 1px, transparent 0);
      background-size: 24px 24px;
    }
    #viewport:active { cursor: grabbing; }

    #canvas {
      position: absolute;
      top: 0; left: 0;
      transform-origin: 0 0;
      width: 2400px;
      height: 1200px;
    }

    svg.connections {
      position: absolute;
      top: 0; left: 0;
      width: 100%;
      height: 100%;
      pointer-events: none;
      z-index: 5;
    }

    .entity-card {
      position: absolute;
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 8px;
      box-shadow: 0 8px 24px rgba(0,0,0,0.5);
      z-index: 10;
      font-size: 11px;
      overflow: hidden;
      transition: transform 0.15s, box-shadow 0.15s, border-color 0.15s;
    }
    .entity-card:hover {
      border-color: #38bdf8;
      box-shadow: 0 12px 32px rgba(56, 189, 248, 0.25);
      z-index: 20;
    }
    .entity-card.hub {
      border: 2px solid #f59e0b;
      box-shadow: 0 0 35px rgba(245, 158, 11, 0.45);
      z-index: 15;
    }
    .entity-card.hub:hover {
      box-shadow: 0 0 50px rgba(245, 158, 11, 0.7);
    }

    .entity-header {
      padding: 6px 10px;
      font-weight: 700;
      font-size: 11px;
      border-bottom: 1px solid rgba(255,255,255,0.1);
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .entity-body {
      padding: 6px 10px;
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .field-row {
      display: flex;
      justify-content: space-between;
      color: #cbd5e1;
    }
    .field-name { display: flex; align-items: center; gap: 4px; }
    .tag-pk { color: #fbbf24; font-weight: 700; }
    .tag-fk { color: #60a5fa; font-weight: 700; }
    .tag-new-fk { color: #f97316; font-weight: 700; background: rgba(249,115,22,0.15); padding: 0 3px; border-radius: 3px; }
    .field-type { color: #64748b; font-family: monospace; font-size: 10px; }

    /* SVG edge styling */
    .edge-line {
      fill: none;
      stroke-linecap: round;
      stroke-linejoin: round;
      transition: stroke-width 0.2s, stroke 0.2s;
    }
    .edge-label {
      font-size: 9px;
      fill: #94a3b8;
      font-family: monospace;
      paint-order: stroke;
      stroke: #090d16;
      stroke-width: 3px;
    }
    .edge-line.highlight {
      stroke-width: 3.5px !important;
      filter: drop-shadow(0 0 6px currentColor);
    }
  </style>
</head>
<body>
  <header>
    <div class="header-title">
      <h1>🎮 Hold My Hand — Kiến Trúc ERD Hoàn Chỉnh</h1>
      <p>Mô hình dữ liệu đặt Game_Session làm Single Source of Truth cho trận đấu Co-op</p>
    </div>

    <div class="legend">
      <div class="legend-item"><span class="badge-dot" style="background:#f59e0b"></span> <b>Game_Session Hub</b></div>
      <div class="legend-item"><span class="badge-dot" style="background:#f97316"></span> [FK*] Quan hệ Mới bổ sung</div>
      <div class="legend-item"><span class="badge-dot" style="background:#38bdf8"></span> Quan hệ Core Session</div>
      <div class="legend-item"><span class="badge-dot" style="background:#71717a"></span> Quan hệ Master Data</div>
    </div>

    <div class="controls">
      <button class="btn" onclick="zoomIn()">➕ Phóng to</button>
      <button class="btn" onclick="zoomOut()">➖ Thu nhỏ</button>
      <button class="btn btn-primary" onclick="resetView()">🎯 Căn giữa</button>
    </div>
  </header>

  <div id="viewport">
    <div id="canvas">
      <svg class="connections" id="svgConnections"></svg>
      <div id="entitiesContainer"></div>
    </div>
  </div>

  <script>
    const data = ${jsonData};
    const container = document.getElementById('entitiesContainer');
    const svg = document.getElementById('svgConnections');
    const viewport = document.getElementById('viewport');
    const canvas = document.getElementById('canvas');

    let scale = 0.85;
    let panX = 40;
    let panY = 20;
    let isDragging = false;
    let startX, startY;

    function renderEntities() {
      container.innerHTML = '';
      data.entities.forEach(ent => {
        const div = document.createElement('div');
        div.className = 'entity-card' + (ent.isHub ? ' hub' : '');
        div.id = 'node-' + ent.id;
        div.style.left = ent.x + 'px';
        div.style.top = ent.y + 'px';
        div.style.width = ent.w + 'px';

        let fieldsHtml = ent.fields.map(f => {
          let tag = '';
          if (f.isPk) tag = '<span class="tag-pk">[PK]</span>';
          else if (f.isFk && f.isNew) tag = '<span class="tag-new-fk">[FK*]</span>';
          else if (f.isFk) tag = '<span class="tag-fk">[FK]</span>';
          return \`
            <div class="field-row">
              <span class="field-name">\${tag} \${f.name}</span>
              <span class="field-type">\${f.type}</span>
            </div>
          \`;
        }).join('');

        div.innerHTML = \`
          <div class="entity-header" style="background:\${ent.headerColor}">
            <span>\${ent.title}</span>
          </div>
          <div class="entity-body">
            \${fieldsHtml}
          </div>
        \`;
        container.appendChild(div);
      });
    }

    function renderEdges() {
      svg.innerHTML = '';
      data.edges.forEach(edge => {
        const fromEnt = data.entities.find(e => e.id === edge.from);
        const toEnt = data.entities.find(e => e.id === edge.to);
        if (!fromEnt || !toEnt) return;

        // Calculate anchors
        const p1 = { x: fromEnt.x + fromEnt.w / 2, y: fromEnt.y + fromEnt.h / 2 };
        const p2 = { x: toEnt.x + toEnt.w / 2, y: toEnt.y + toEnt.h / 2 };

        let stroke = '#52525b';
        let strokeWidth = 1.2;
        let strokeDash = '';

        if (edge.isCore && edge.isNew) {
          stroke = '#f59e0b';
          strokeWidth = 2.5;
          strokeDash = '5,4';
        } else if (edge.isCore) {
          stroke = '#38bdf8';
          strokeWidth = 2;
        } else if (edge.isNew) {
          stroke = '#ec4899';
          strokeWidth = 1.8;
          strokeDash = '4,4';
        }

        // Mid curve or orthogonal
        const midY = (p1.y + p2.y) / 2;
        const d = \`M \${p1.x} \${p1.y} C \${p1.x} \${midY}, \${p2.x} \${midY}, \${p2.x} \${p2.y}\`;

        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('d', d);
        path.setAttribute('class', 'edge-line' + (edge.isCore ? ' core-edge' : ''));
        path.setAttribute('stroke', stroke);
        path.setAttribute('stroke-width', strokeWidth);
        if (strokeDash) path.setAttribute('stroke-dasharray', strokeDash);
        svg.appendChild(path);

        if (edge.label) {
          const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
          text.setAttribute('x', (p1.x + p2.x) / 2);
          text.setAttribute('y', midY);
          text.setAttribute('text-anchor', 'middle');
          text.setAttribute('class', 'edge-label');
          text.textContent = edge.label;
          svg.appendChild(text);
        }
      });
    }

    function updateTransform() {
      canvas.style.transform = \`translate(\${panX}px, \${panY}px) scale(\${scale})\`;
    }

    function zoomIn() { scale = Math.min(2.0, scale + 0.1); updateTransform(); }
    function zoomOut() { scale = Math.max(0.4, scale - 0.1); updateTransform(); }
    function resetView() {
      scale = 0.8;
      panX = 30;
      panY = 15;
      updateTransform();
    }

    viewport.addEventListener('mousedown', e => {
      isDragging = true;
      startX = e.clientX - panX;
      startY = e.clientY - panY;
    });

    window.addEventListener('mousemove', e => {
      if (!isDragging) return;
      panX = e.clientX - startX;
      panY = e.clientY - startY;
      updateTransform();
    });

    window.addEventListener('mouseup', () => { isDragging = false; });
    viewport.addEventListener('wheel', e => {
      e.preventDefault();
      const zoomFactor = e.deltaY < 0 ? 1.08 : 0.92;
      scale = Math.max(0.4, Math.min(2.0, scale * zoomFactor));
      updateTransform();
    });

    renderEntities();
    renderEdges();
    updateTransform();
  </script>
</body>
</html>`;
}

// Generate files
const drawioContent = generateDrawioXml();
const htmlContent = generateHtmlViewer();

const docsDir = path.resolve('d:/holdmyhand/Hold-My-Hand-Game-Development/Docs');
fs.writeFileSync(path.join(docsDir, 'HoldMyHand_ERD.drawio'), drawioContent, 'utf8');
fs.writeFileSync(path.join(docsDir, 'HoldMyHand_ERD_Viewer.html'), htmlContent, 'utf8');

console.log('Successfully generated Docs/HoldMyHand_ERD.drawio and Docs/HoldMyHand_ERD_Viewer.html');
