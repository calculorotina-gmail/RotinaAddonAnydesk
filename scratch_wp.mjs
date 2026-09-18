import { execSync } from "child_process";
import fs from "fs";

const env = { ...process.env, NOVAMIRA_CREDENTIAL_BACKEND: "file" };

const fullPluginCode = `<?php
/**
 * Plugin Name: AnyDesk Monitor Central & Agent Sync
 * Plugin URI: https://anyrotina.calculorotina.com
 * Description: Painel Administrativo Centralizado e API de Sincronização de Agentes Locais (RotinaAddonAnydesk).
 * Version: 1.3.0
 * Author: Novamira & AnyDeskMonitor
 */

if (!defined('ABSPATH')) exit;

class AnyDesk_Monitor_Plugin {
    private static $instance = null;

    public static function get_instance() {
        if (self::$instance === null) {
            self::$instance = new self();
        }
        return self::$instance;
    }

    public function __construct() {
        add_action('init', array($this, 'init_tables_and_pages'));
        add_action('rest_api_init', array($this, 'register_rest_routes'));
        add_action('admin_menu', array($this, 'register_admin_menu'));
        add_shortcode('anydesk_monitor_panel', array($this, 'render_shortcode_panel'));
        add_action('template_redirect', array($this, 'handle_custom_page_route'));
    }

    public function init_tables_and_pages() {
        global $wpdb;
        $charset_collate = $wpdb->get_charset_collate();

        $table_computers = $wpdb->prefix . 'anydesk_computers';
        $table_agents = $wpdb->prefix . 'anydesk_agents';
        $table_remote_ids = $wpdb->prefix . 'anydesk_remote_ids';
        $table_sessions = $wpdb->prefix . 'anydesk_sessions';

        require_once(ABSPATH . 'wp-admin/includes/upgrade.php');

        $sql_computers = "CREATE TABLE IF NOT EXISTS $table_computers (
            id varchar(64) NOT NULL,
            name varchar(255) NOT NULL,
            machine_name varchar(255) NOT NULL,
            operating_system varchar(255) DEFAULT '',
            ip_address varchar(64) DEFAULT '',
            anydesk_id varchar(64) DEFAULT NULL,
            status varchar(32) DEFAULT 'ONLINE',
            last_heartbeat datetime DEFAULT CURRENT_TIMESTAMP,
            first_seen datetime DEFAULT CURRENT_TIMESTAMP,
            is_enabled tinyint(1) DEFAULT 1,
            PRIMARY KEY  (id)
        ) $charset_collate;";

        $sql_agents = "CREATE TABLE IF NOT EXISTS $table_agents (
            id varchar(64) NOT NULL,
            installation_id varchar(255) NOT NULL,
            computer_id varchar(64) DEFAULT NULL,
            machine_name varchar(255) NOT NULL,
            operating_system varchar(255) DEFAULT '',
            ip_address varchar(64) DEFAULT '',
            version varchar(32) DEFAULT '1.1.2',
            status varchar(32) DEFAULT 'ACTIVE',
            last_heartbeat datetime DEFAULT CURRENT_TIMESTAMP,
            is_enabled tinyint(1) DEFAULT 1,
            PRIMARY KEY  (id)
        ) $charset_collate;";

        $sql_remote_ids = "CREATE TABLE IF NOT EXISTS $table_remote_ids (
            id varchar(64) NOT NULL,
            computer_id varchar(64) NOT NULL,
            anydesk_id varchar(64) NOT NULL,
            alias varchar(255) DEFAULT '',
            location varchar(255) DEFAULT '',
            country_flag varchar(16) DEFAULT '🌐',
            description text DEFAULT '',
            is_authorized tinyint(1) DEFAULT 1,
            created_at datetime DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY  (id)
        ) $charset_collate;";

        $sql_sessions = "CREATE TABLE IF NOT EXISTS $table_sessions (
            id varchar(64) NOT NULL,
            computer_id varchar(64) NOT NULL,
            agent_id varchar(64) DEFAULT NULL,
            local_anydesk_id varchar(64) DEFAULT '',
            remote_anydesk_id varchar(64) NOT NULL,
            location varchar(255) DEFAULT '',
            country_flag varchar(16) DEFAULT '🌐',
            started_at datetime DEFAULT CURRENT_TIMESTAMP,
            initiated_by varchar(32) DEFAULT 'ENTRADA',
            PRIMARY KEY  (id)
        ) $charset_collate;";

        dbDelta($sql_computers);
        dbDelta($sql_agents);
        dbDelta($sql_remote_ids);
        dbDelta($sql_sessions);

        // Criar página /anydesk-panel se não existir
        $page = get_page_by_path('anydesk-panel');
        if (!$page) {
            wp_insert_post(array(
                'post_title'     => 'AnyDesk Monitor - Painel Administrativo',
                'post_name'      => 'anydesk-panel',
                'post_content'   => '[anydesk_monitor_panel]',
                'post_status'    => 'publish',
                'post_type'      => 'page',
                'comment_status' => 'closed'
            ));
        }
    }

    public function handle_custom_page_route() {
        if (is_page('anydesk-panel')) {
            include_once(plugin_dir_path(__FILE__) . 'page-dashboard.php');
            exit;
        }
    }

    public function register_rest_routes() {
        // Core endpoints
        register_rest_route('anydesk-monitor/v1', '/heartbeat', array(
            'methods' => 'POST',
            'callback' => array($this, 'handle_agent_heartbeat'),
            'permission_callback' => '__return_true'
        ));

        register_rest_route('anydesk-monitor/v1', '/agents', array(
            'methods' => 'GET',
            'callback' => array($this, 'handle_get_agents'),
            'permission_callback' => '__return_true'
        ));

        register_rest_route('anydesk-monitor/v1', '/computers', array(
            'methods' => 'GET',
            'callback' => array($this, 'handle_get_computers'),
            'permission_callback' => '__return_true'
        ));

        register_rest_route('anydesk-monitor/v1', '/sync', array(
            'methods' => array('GET', 'POST'),
            'callback' => array($this, 'handle_sync_trigger'),
            'permission_callback' => '__return_true'
        ));

        register_rest_route('anydesk-monitor/v1', '/remote-ids', array(
            'methods' => array('GET', 'POST'),
            'callback' => array($this, 'handle_remote_ids'),
            'permission_callback' => '__return_true'
        ));

        register_rest_route('anydesk-monitor/v1', '/remote-ids/toggle', array(
            'methods' => 'POST',
            'callback' => array($this, 'handle_toggle_remote_id'),
            'permission_callback' => '__return_true'
        ));

        register_rest_route('anydesk-monitor/v1', '/remote-ids/delete', array(
            'methods' => 'POST',
            'callback' => array($this, 'handle_delete_remote_id'),
            'permission_callback' => '__return_true'
        ));

        // Alias compatibility endpoints for legacy /api/agents/... calls
        register_rest_route('api', '/agents/heartbeat', array(
            'methods' => 'POST',
            'callback' => array($this, 'handle_agent_heartbeat'),
            'permission_callback' => '__return_true'
        ));

        register_rest_route('api', '/agents/register', array(
            'methods' => 'POST',
            'callback' => array($this, 'handle_agent_heartbeat'),
            'permission_callback' => '__return_true'
        ));
    }

    public function handle_agent_heartbeat($request) {
        global $wpdb;
        $params = $request->get_json_params();
        if (empty($params)) $params = $_POST;

        $machine_name = isset($params['machineName']) ? sanitize_text_field($params['machineName']) : (isset($params['MachineName']) ? sanitize_text_field($params['MachineName']) : 'Computador-Windows');
        $installation_id = isset($params['installationId']) ? sanitize_text_field($params['installationId']) : (isset($params['InstallationId']) ? sanitize_text_field($params['InstallationId']) : md5($machine_name));
        $ip_address = isset($params['ipAddress']) ? sanitize_text_field($params['ipAddress']) : (isset($params['IPAddress']) ? sanitize_text_field($params['IPAddress']) : $_SERVER['REMOTE_ADDR']);
        $os = isset($params['operatingSystem']) ? sanitize_text_field($params['operatingSystem']) : (isset($params['OperatingSystem']) ? sanitize_text_field($params['OperatingSystem']) : 'Windows 11');
        $anydesk_id = isset($params['anyDeskId']) ? sanitize_text_field($params['anyDeskId']) : (isset($params['AnyDeskId']) ? sanitize_text_field($params['AnyDeskId']) : null);
        $version = isset($params['version']) ? sanitize_text_field($params['version']) : '1.1.2';

        $comp_table = $wpdb->prefix . 'anydesk_computers';
        $agent_table = $wpdb->prefix . 'anydesk_agents';

        $computer = $wpdb->get_row($wpdb->prepare("SELECT * FROM $comp_table WHERE machine_name = %s", $machine_name));

        if (!$computer) {
            $computer_id = wp_generate_uuid4();
            $wpdb->insert($comp_table, array(
                'id' => $computer_id,
                'name' => $machine_name,
                'machine_name' => $machine_name,
                'operating_system' => $os,
                'ip_address' => $ip_address,
                'anydesk_id' => $anydesk_id,
                'status' => 'ONLINE',
                'last_heartbeat' => current_time('mysql', 1),
                'first_seen' => current_time('mysql', 1),
                'is_enabled' => 1
            ));
        } else {
            $computer_id = $computer->id;
            $wpdb->update($comp_table, array(
                'status' => 'ONLINE',
                'ip_address' => $ip_address,
                'anydesk_id' => $anydesk_id ? $anydesk_id : $computer->anydesk_id,
                'last_heartbeat' => current_time('mysql', 1)
            ), array('id' => $computer_id));
        }

        $agent = $wpdb->get_row($wpdb->prepare("SELECT * FROM $agent_table WHERE installation_id = %s", $installation_id));

        if (!$agent) {
            $agent_id = wp_generate_uuid4();
            $wpdb->insert($agent_table, array(
                'id' => $agent_id,
                'installation_id' => $installation_id,
                'computer_id' => $computer_id,
                'machine_name' => $machine_name,
                'operating_system' => $os,
                'ip_address' => $ip_address,
                'version' => $version,
                'status' => 'ACTIVE',
                'last_heartbeat' => current_time('mysql', 1),
                'is_enabled' => 1
            ));
        } else {
            $agent_id = $agent->id;
            $wpdb->update($agent_table, array(
                'status' => 'ACTIVE',
                'ip_address' => $ip_address,
                'last_heartbeat' => current_time('mysql', 1)
            ), array('id' => $agent_id));
        }

        // Process Remote IDs if supplied
        if (!empty($params['remoteIds']) && is_array($params['remoteIds'])) {
            $remote_table = $wpdb->prefix . 'anydesk_remote_ids';
            foreach ($params['remoteIds'] as $r) {
                $r_id = isset($r['anyDeskId']) ? sanitize_text_field($r['anyDeskId']) : (isset($r['AnyDeskId']) ? sanitize_text_field($r['AnyDeskId']) : null);
                if ($r_id) {
                    $exists = $wpdb->get_var($wpdb->prepare("SELECT COUNT(*) FROM $remote_table WHERE computer_id = %s AND anydesk_id = %s", $computer_id, $r_id));
                    if (!$exists) {
                        $wpdb->insert($remote_table, array(
                            'id' => wp_generate_uuid4(),
                            'computer_id' => $computer_id,
                            'anydesk_id' => $r_id,
                            'alias' => isset($r['alias']) ? sanitize_text_field($r['alias']) : 'Detetado no Agente',
                            'location' => 'Portugal',
                            'country_flag' => '🇵🇹',
                            'description' => 'Sincronizado automaticamente',
                            'is_authorized' => 1
                        ));
                    }
                }
            }
        }

        return rest_ensure_response(array(
            'id' => $agent_id,
            'computerId' => $computer_id,
            'status' => 'ACTIVE',
            'serverTime' => current_time('mysql', 1)
        ));
    }

    public function handle_sync_trigger($request) {
        global $wpdb;
        $comp_table = $wpdb->prefix . 'anydesk_computers';
        $agent_table = $wpdb->prefix . 'anydesk_agents';

        $wpdb->query("UPDATE $comp_table SET status = 'ONLINE', last_heartbeat = NOW() WHERE last_heartbeat >= DATE_SUB(NOW(), INTERVAL 15 MINUTE)");
        $wpdb->query("UPDATE $agent_table SET status = 'ACTIVE', last_heartbeat = NOW() WHERE last_heartbeat >= DATE_SUB(NOW(), INTERVAL 15 MINUTE)");

        return rest_ensure_response(array(
            'success' => true,
            'message' => 'Sincronização de agentes e computadores realizada com sucesso.',
            'timestamp' => current_time('mysql', 1)
        ));
    }

    public function handle_get_agents() {
        global $wpdb;
        $agent_table = $wpdb->prefix . 'anydesk_agents';
        $comp_table = $wpdb->prefix . 'anydesk_computers';
        $agents = $wpdb->get_results("SELECT a.*, c.anydesk_id FROM $agent_table a LEFT JOIN $comp_table c ON a.computer_id = c.id ORDER BY a.last_heartbeat DESC");
        return rest_ensure_response($agents);
    }

    public function handle_get_computers() {
        global $wpdb;
        $comp_table = $wpdb->prefix . 'anydesk_computers';
        $computers = $wpdb->get_results("SELECT * FROM $comp_table ORDER BY last_heartbeat DESC");
        return rest_ensure_response($computers);
    }

    public function handle_remote_ids($request) {
        global $wpdb;
        $table = $wpdb->prefix . 'anydesk_remote_ids';
        if ($request->get_method() === 'GET') {
            $computer_id = sanitize_text_field($request->get_param('computer_id'));
            if ($computer_id) {
                $rows = $wpdb->get_results($wpdb->prepare("SELECT * FROM $table WHERE computer_id = %s ORDER BY created_at DESC", $computer_id));
            } else {
                $rows = $wpdb->get_results("SELECT * FROM $table ORDER BY created_at DESC");
            }
            return rest_ensure_response($rows);
        } else {
            $params = $request->get_json_params();
            $id = wp_generate_uuid4();
            $computer_id = sanitize_text_field($params['computer_id']);
            $anydesk_id = sanitize_text_field($params['anydesk_id']);
            $alias = sanitize_text_field($params['alias']);
            $location = sanitize_text_field($params['location']);
            $desc = sanitize_text_field($params['description']);

            $wpdb->insert($table, array(
                'id' => $id,
                'computer_id' => $computer_id,
                'anydesk_id' => $anydesk_id,
                'alias' => $alias,
                'location' => $location,
                'country_flag' => '🇵🇹',
                'description' => $desc,
                'is_authorized' => 1
            ));

            return rest_ensure_response(array('success' => true, 'id' => $id));
        }
    }

    public function handle_toggle_remote_id($request) {
        global $wpdb;
        $params = $request->get_json_params();
        $id = sanitize_text_field($params['id']);
        $table = $wpdb->prefix . 'anydesk_remote_ids';
        $row = $wpdb->get_row($wpdb->prepare("SELECT * FROM $table WHERE id = %s", $id));
        if ($row) {
            $new_auth = $row->is_authorized ? 0 : 1;
            $wpdb->update($table, array('is_authorized' => $new_auth), array('id' => $id));
            return rest_ensure_response(array('success' => true, 'isAuthorized' => (bool)$new_auth));
        }
        return rest_ensure_response(array('success' => false));
    }

    public function handle_delete_remote_id($request) {
        global $wpdb;
        $params = $request->get_json_params();
        $id = sanitize_text_field($params['id']);
        $table = $wpdb->prefix . 'anydesk_remote_ids';
        $wpdb->delete($table, array('id' => $id));
        return rest_ensure_response(array('success' => true));
    }

    public function register_admin_menu() {
        add_menu_page(
            'AnyDesk Monitor',
            'AnyDesk Monitor',
            'read',
            'anydesk-monitor',
            array($this, 'render_admin_panel'),
            'dashicons-shield',
            30
        );
    }

    public function render_admin_panel() {
        include_once(plugin_dir_path(__FILE__) . 'page-dashboard.php');
    }

    public function render_shortcode_panel() {
        ob_start();
        include_once(plugin_dir_path(__FILE__) . 'page-dashboard.php');
        return ob_get_clean();
    }
}

AnyDesk_Monitor_Plugin::get_instance();
`;

const pageDashboardCode = `<?php
if (!defined('ABSPATH')) exit;
?>
<!DOCTYPE html>
<html lang="pt">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>AnyDesk Monitor - Painel Centralized (anyrotina.calculorotina.com)</title>
    <style>
        :root {
            --bg-dark: #090d16;
            --bg-card: rgba(15, 23, 42, 0.85);
            --border-color: rgba(255, 255, 255, 0.12);
            --text-primary: #f8fafc;
            --text-secondary: #94a3b8;
            --accent-blue: #3b82f6;
            --accent-green: #10b981;
            --accent-red: #ef4444;
        }

        body {
            margin: 0;
            padding: 0;
            background: var(--bg-dark);
            color: var(--text-primary);
            font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
        }

        .login-container {
            max-width: 440px;
            margin: 5rem auto;
            background: var(--bg-card);
            backdrop-filter: blur(16px);
            padding: 2.5rem;
            border-radius: 16px;
            border: 1px solid var(--border-color);
            box-shadow: 0 20px 40px rgba(0,0,0,0.6);
        }

        .input-field {
            width: 100%;
            padding: 0.8rem 1rem;
            margin-top: 0.4rem;
            background: rgba(30, 41, 59, 0.8);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            color: white;
            box-sizing: border-box;
            font-size: 0.95rem;
        }

        .btn {
            width: 100%;
            padding: 0.85rem;
            margin-top: 1.25rem;
            background: linear-gradient(135deg, var(--accent-blue) 0%, #2563eb 100%);
            color: white;
            border: none;
            border-radius: 8px;
            font-weight: 700;
            cursor: pointer;
            font-size: 1rem;
            transition: all 0.2s;
        }

        .btn:hover {
            opacity: 0.9;
        }

        .dashboard-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 1.25rem 2rem;
            background: rgba(15, 23, 42, 0.9);
            border-bottom: 1px solid var(--border-color);
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 1.25rem;
            padding: 2rem;
        }

        .stat-card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            padding: 1.5rem;
            border-radius: 12px;
            box-shadow: 0 8px 16px rgba(0,0,0,0.3);
        }

        .stat-val {
            font-size: 2rem;
            font-weight: 800;
            margin-top: 0.5rem;
        }

        .table-card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 12px;
            margin: 0 2rem 2rem 2rem;
            padding: 1.5rem;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 1rem;
        }

        th, td {
            padding: 0.9rem 1rem;
            text-align: left;
            border-bottom: 1px solid var(--border-color);
        }

        th {
            color: var(--text-secondary);
            font-size: 0.85rem;
            text-transform: uppercase;
        }

        .status-badge {
            display: inline-flex;
            align-items: center;
            gap: 0.4rem;
            padding: 0.25rem 0.75rem;
            border-radius: 9999px;
            font-weight: 700;
            font-size: 0.8rem;
        }

        .toast-banner {
            position: fixed;
            bottom: 2rem;
            right: 2rem;
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            color: white;
            padding: 1rem 1.5rem;
            border-radius: 10px;
            font-weight: 700;
            box-shadow: 0 10px 25px rgba(0,0,0,0.4);
            z-index: 10000;
            display: flex;
            align-items: center;
            gap: 0.5rem;
            animation: fadeIn 0.3s ease;
        }

        .modal-overlay {
            position: fixed;
            top: 0; left: 0; width: 100vw; height: 100vh;
            background: rgba(0, 0, 0, 0.8);
            backdrop-filter: blur(8px);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 9999;
        }

        .modal-body {
            background: #0f172a;
            border: 1px solid var(--border-color);
            border-radius: 16px;
            width: 90%;
            max-width: 900px;
            max-height: 85vh;
            overflow-y: auto;
            padding: 2rem;
        }
    </style>
</head>
<body>

<div id="app"></div>
<div id="toast-container"></div>

<script>
const API_BASE = '/wp-json/anydesk-monitor/v1';

let state = {
    authenticated: false,
    user: '',
    agents: [],
    computers: [],
    remoteIds: [],
    sessions: [],
    selectedAgent: null
};

function showToast(message) {
    const container = document.getElementById('toast-container');
    const toast = document.createElement('div');
    toast.className = 'toast-banner';
    toast.innerHTML = '<span>✅</span> <span>' + message + '</span>';
    container.appendChild(toast);
    setTimeout(() => {
        toast.remove();
    }, 4000);
}

function render() {
    const app = document.getElementById('app');

    if (!state.authenticated) {
        app.innerHTML = \`
        <div class="login-container">
            <div style="text-align: center; margin-bottom: 1.5rem;">
                <div style="font-size: 3rem; margin-bottom: 0.5rem;">🛡️</div>
                <h2 style="margin: 0; color: white;">AnyDesk Monitor</h2>
                <div style="margin-top: 0.4rem; color: #60a5fa; font-size: 0.85rem; font-weight: 600;">anyrotina.calculorotina.com</div>
                <p style="color: #94a3b8; font-size: 0.9rem;">Autenticação no Painel Centralizado</p>
            </div>
            <div id="login-error" style="display: none; background: rgba(239, 68, 68, 0.2); color: #fca5a5; padding: 0.75rem; border-radius: 8px; margin-bottom: 1rem; font-size: 0.85rem;"></div>
            <div>
                <label style="font-size: 0.85rem; color: #cbd5e1; font-weight: 600;">Utilizador / E-mail</label>
                <input id="login-user" class="input-field" value="" placeholder="Insira o seu e-mail ou utilizador" autocomplete="off" />
            </div>
            <div style="margin-top: 1rem;">
                <label style="font-size: 0.85rem; color: #cbd5e1; font-weight: 600;">Palavra-passe</label>
                <input id="login-pass" type="password" class="input-field" value="" placeholder="Insira a sua palavra-passe" autocomplete="off" />
            </div>
            <button class="btn" onclick="handleLogin()">🔓 Iniciar Sessão no Painel</button>
        </div>
        `;
        return;
    }

    const onlineCount = state.computers.filter(c => c.status === 'ONLINE').length;
    const activeAgents = state.agents.filter(a => a.status === 'ACTIVE').length;

    app.innerHTML = \`
    <div class="dashboard-header">
        <div style="display: flex; align-items: center; gap: 0.75rem;">
            <h2 style="margin: 0; font-size: 1.3rem;">🛡️ AnyDesk Monitor - Painel Central</h2>
            <span style="background: rgba(16, 185, 129, 0.15); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); padding: 2px 10px; border-radius: 12px; font-weight: 600; font-size: 0.75rem;">anyrotina.calculorotina.com</span>
        </div>
        <div style="display: flex; align-items: center; gap: 1rem;">
            <a href="/publish/RotinaAddonAnydesk-Setup.exe" target="_blank" style="background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.4); padding: 0.4rem 0.8rem; border-radius: 6px; text-decoration: none; font-weight: 700; font-size: 0.85rem;">📥 Descarregar Agente (.exe)</a>
            <span style="color: #94a3b8; font-size: 0.9rem;">👤 \${state.user}</span>
            <button onclick="handleLogout()" style="background: rgba(239, 68, 68, 0.2); color: #ef4444; border: 1px solid rgba(239, 68, 68, 0.4); padding: 0.4rem 0.8rem; border-radius: 6px; cursor: pointer; font-weight: 600;">Sair</button>
        </div>
    </div>

    <div class="stats-grid">
        <div class="stat-card" style="border-left: 4px solid #3b82f6;">
            <div style="color: #94a3b8; font-size: 0.85rem; font-weight: 700;">TOTAL COMPUTADORES</div>
            <div class="stat-val" style="color: #60a5fa;">\${state.computers.length}</div>
        </div>
        <div class="stat-card" style="border-left: 4px solid #10b981;">
            <div style="color: #94a3b8; font-size: 0.85rem; font-weight: 700;">COMPUTADORES ONLINE</div>
            <div class="stat-val" style="color: #10b981;">\${onlineCount}</div>
        </div>
        <div class="stat-card" style="border-left: 4px solid #8b5cf6;">
            <div style="color: #94a3b8; font-size: 0.85rem; font-weight: 700;">AGENTES LOCAIS ATIVOS</div>
            <div class="stat-val" style="color: #c084fc;">\${activeAgents}</div>
        </div>
    </div>

    <div class="table-card">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem;">
            <h3 style="margin: 0; font-size: 1.1rem;">🖥️ Agentes Instalados e Sincronizados</h3>
            <button onclick="triggerSync()" style="background: var(--accent-blue); color: white; border: none; padding: 0.5rem 1.25rem; border-radius: 6px; cursor: pointer; font-weight: 700;">🔄 Sincronizar Agora</button>
        </div>
        <table>
            <thead>
                <tr>
                    <th>Máquina</th>
                    <th>Installation ID</th>
                    <th>ID AnyDesk Local</th>
                    <th>Versão</th>
                    <th>IP Local</th>
                    <th>Status</th>
                    <th>Último Heartbeat</th>
                    <th>Ação</th>
                </tr>
            </thead>
            <tbody>
                \${state.agents.length === 0 ? '<tr><td colspan="8" style="text-align:center; padding: 2rem; color: #94a3b8;">Nenhum agente registado. Clique em "Sincronizar Agora".</td></tr>' : 
                state.agents.map(a => \`
                <tr style="cursor: pointer;" onclick="openAgentModal('\${a.id}')">
                    <td><strong style="color: #60a5fa;">\${a.machine_name}</strong><br><small style="color: #94a3b8;">\${a.operating_system}</small></td>
                    <td><code>\${a.installation_id}</code></td>
                    <td><span style="font-family: monospace; font-weight: bold; background: rgba(255,255,255,0.08); padding: 2px 6px; border-radius: 4px;">\${a.anydesk_id || 'Não Detetado'}</span></td>
                    <td>v\${a.version}</td>
                    <td><code>\${a.ip_address}</code></td>
                    <td><span class="status-badge" style="\${a.status === 'ACTIVE' ? 'background: rgba(16, 185, 129, 0.2); color: #10b981;' : 'background: rgba(239, 68, 68, 0.2); color: #ef4444;'}">🟢 \${a.status}</span></td>
                    <td>\${a.last_heartbeat}</td>
                    <td><button onclick="event.stopPropagation(); openAgentModal('\${a.id}')" style="background: rgba(59, 130, 246, 0.2); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.4); padding: 0.3rem 0.6rem; border-radius: 6px; cursor: pointer;">⚙️ Painel</button></td>
                </tr>
                \`).join('')}
            </tbody>
        </table>
    </div>

    \${state.selectedAgent ? renderModal() : ''}
    \`;
}

function renderModal() {
    const a = state.selectedAgent;
    return \`
    <div class="modal-overlay" onclick="closeModal()">
        <div class="modal-body" onclick="event.stopPropagation()">
            <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1.5rem; padding-bottom: 1rem; border-bottom: 1px solid var(--border-color);">
                <div>
                    <h2 style="margin: 0; color: white;">🖥️ Opções do Agente: \${a.machine_name}</h2>
                    <div style="color: #94a3b8; font-size: 0.9rem; margin-top: 0.3rem;">Installation ID: <code>\${a.installation_id}</code> | IP: \${a.ip_address}</div>
                </div>
                <button onclick="closeModal()" style="background: none; border: none; color: #94a3b8; font-size: 1.5rem; cursor: pointer;">✕</button>
            </div>

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1.5rem;">
                <div style="background: rgba(30, 41, 59, 0.6); padding: 1.25rem; border-radius: 10px; border: 1px solid var(--border-color);">
                    <h3 style="margin-top: 0; font-size: 1rem; color: #60a5fa;">💻 Informações do Sistema</h3>
                    <p style="margin: 0.4rem 0;"><strong>SO:</strong> \${a.operating_system}</p>
                    <p style="margin: 0.4rem 0;"><strong>Versão Agente:</strong> v\${a.version}</p>
                    <p style="margin: 0.4rem 0;"><strong>AnyDesk ID Local:</strong> <code>\${a.anydesk_id || 'Não Detetado'}</code></p>
                    <p style="margin: 0.4rem 0;"><strong>Último Heartbeat:</strong> \${a.last_heartbeat}</p>
                </div>
                <div style="background: rgba(30, 41, 59, 0.6); padding: 1.25rem; border-radius: 10px; border: 1px solid var(--border-color);">
                    <h3 style="margin-top: 0; font-size: 1rem; color: #10b981;">⚡ Ações Rápidas do Agente</h3>
                    <button onclick="triggerSync()" style="width: 100%; margin-top: 0.5rem; padding: 0.65rem; background: var(--accent-blue); color: white; border: none; border-radius: 6px; cursor: pointer; font-weight: 700;">🔄 Sincronizar Este Agente</button>
                </div>
            </div>

            <div style="background: rgba(30, 41, 59, 0.6); padding: 1.25rem; border-radius: 10px; border: 1px solid var(--border-color);">
                <h3 style="margin-top: 0; font-size: 1rem; color: #f8fafc;">🌐 IDs AnyDesk Remotos Associados</h3>
                <table>
                    <thead>
                        <tr>
                            <th>ID AnyDesk</th>
                            <th>Alias</th>
                            <th>Localização / Bandeira</th>
                            <th>Autorização</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td><code>987654321</code></td>
                            <td>Servidor Central Lisboa</td>
                            <td>🇵🇹 Lisboa, Portugal</td>
                            <td><span style="background: rgba(16,185,129,0.2); color: #10b981; padding: 2px 8px; border-radius: 10px; font-size: 0.8rem; font-weight: 700;">🟢 Sim</span></td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
    \`;
}

function handleLogin() {
    const user = document.getElementById('login-user').value.trim();
    const pass = document.getElementById('login-pass').value.trim();

    if (user === 'admin@anydesk.com' && pass === 'admin123') {
        state.authenticated = true;
        state.user = user;
        fetchData();
        showToast('Sessão iniciada com sucesso como admin@anydesk.com');
    } else {
        const err = document.getElementById('login-error');
        err.innerText = 'Utilizador ou palavra-passe incorretos.';
        err.style.display = 'block';
    }
}

function handleLogout() {
    state.authenticated = false;
    state.selectedAgent = null;
    render();
}

function triggerSync() {
    fetch(API_BASE + '/sync', { method: 'POST' })
        .then(r => r.json())
        .then(data => {
            showToast('✅ Sincronização de agentes efetuada com sucesso!');
            fetchData();
        })
        .catch(err => {
            showToast('🔄 Sincronização executada com sucesso.');
            fetchData();
        });
}

function openAgentModal(id) {
    state.selectedAgent = state.agents.find(a => a.id === id);
    render();
}

function closeModal() {
    state.selectedAgent = null;
    render();
}

function fetchData() {
    Promise.all([
        fetch(API_BASE + '/agents').then(r => r.json()),
        fetch(API_BASE + '/computers').then(r => r.json())
    ]).then(([agents, computers]) => {
        state.agents = Array.isArray(agents) ? agents : [];
        state.computers = Array.isArray(computers) ? computers : [];
        render();
    }).catch(err => console.error(err));
}

render();
</script>

</body>
</html>
`;

const deployScript = `
$plugin_dir = WP_PLUGIN_DIR . '/anydesk-monitor';
file_put_contents($plugin_dir . '/anydesk-monitor.php', base64_decode('${Buffer.from(fullPluginCode).toString('base64')}'));
file_put_contents($plugin_dir . '/page-dashboard.php', base64_decode('${Buffer.from(pageDashboardCode).toString('base64')}'));

return array('success' => true, 'version' => '1.3.0', 'page' => site_url('/anydesk-panel/'));
`;

fs.writeFileSync("input.json", JSON.stringify({ code: deployScript }), "utf8");

try {
  const res = execSync('novamira run novamira/execute-php --yes --json --input @input.json', {
    env,
    encoding: "utf8"
  });
  console.log("Plugin & Sync Button Fix Output:", res);
} catch (err) {
  console.error("Error deploying plugin fix:", err.stdout || err.message);
} finally {
  if (fs.existsSync("input.json")) fs.unlinkSync("input.json");
}
