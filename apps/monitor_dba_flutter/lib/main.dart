import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';
import 'package:http/http.dart' as http;
import 'package:url_launcher/url_launcher.dart';

void main() => runApp(const MonitorDbaApp());

class MonitorDbaApp extends StatelessWidget {
  const MonitorDbaApp({super.key});

  @override
  Widget build(BuildContext context) {
    const navy = Color(0xFF08172B);
    const gold = Color(0xFFD4AF37);
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'Monitor DBA',
      theme: ThemeData(
        brightness: Brightness.dark,
        scaffoldBackgroundColor: navy,
        colorScheme: ColorScheme.fromSeed(
          seedColor: gold,
          brightness: Brightness.dark,
          primary: gold,
          surface: const Color(0xFF10233D),
        ),
        cardTheme: const CardThemeData(
          elevation: 0,
          margin: EdgeInsets.zero,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.all(Radius.circular(18)),
            side: BorderSide(color: Color(0x334D6B8D)),
          ),
        ),
        inputDecorationTheme: const InputDecorationTheme(
          border: OutlineInputBorder(),
        ),
        useMaterial3: true,
      ),
      home: const ConnectionScreen(),
    );
  }
}

class ConnectionScreen extends StatefulWidget {
  const ConnectionScreen({super.key});

  @override
  State<ConnectionScreen> createState() => _ConnectionScreenState();
}

class _ConnectionScreenState extends State<ConnectionScreen> {
  final _baseUrl = TextEditingController(text: 'https://monitor.example.com');
  String? _error;

  @override
  void dispose() {
    _baseUrl.dispose();
    super.dispose();
  }

  Future<void> _signIn() async {
    final uri = Uri.tryParse(_baseUrl.text.trim());
    if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
      setState(() => _error = 'Enter a valid Monitor URL, including https://');
      return;
    }
    if (!mounted) return;
    final session = await Navigator.of(context).push<MonitorSession>(
      MaterialPageRoute(builder: (_) => LoginWebView(baseUri: uri)),
    );
    if (session != null && mounted) {
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => DashboardScreen(session: session)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 520),
              child: Card(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Icon(Icons.storage_rounded, size: 54),
                      const SizedBox(height: 16),
                      Text('Monitor DBA', style: Theme.of(context).textTheme.headlineMedium, textAlign: TextAlign.center),
                      const SizedBox(height: 8),
                      const Text(
                        'A simplified read-only dashboard for SQL Server health and DBA actions. Sign-in uses the existing Monitor web session; the app never connects directly to SQL Server.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 28),
                      TextField(
                        controller: _baseUrl,
                        keyboardType: TextInputType.url,
                        autocorrect: false,
                        decoration: const InputDecoration(labelText: 'Monitor base URL', hintText: 'https://monitor.company.local'),
                      ),
                      if (_error != null) ...[
                        const SizedBox(height: 10),
                        Text(_error!, style: const TextStyle(color: Colors.orangeAccent)),
                      ],
                      const SizedBox(height: 18),
                      FilledButton.icon(
                        onPressed: _signIn,
                        icon: const Icon(Icons.login_rounded),
                        label: const Text('Sign in to Monitor'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class MonitorSession {
  const MonitorSession({required this.baseUri, required this.cookieHeader});
  final Uri baseUri;
  final String cookieHeader;
}

class LoginWebView extends StatefulWidget {
  const LoginWebView({super.key, required this.baseUri});
  final Uri baseUri;

  @override
  State<LoginWebView> createState() => _LoginWebViewState();
}

class _LoginWebViewState extends State<LoginWebView> {
  bool _loading = true;
  String? _message;

  Future<void> _useSession() async {
    final cookies = await CookieManager.instance().getCookies(url: WebUri(widget.baseUri.toString()));
    if (cookies.isEmpty) {
      setState(() => _message = 'No authenticated Monitor session cookie is available yet. Sign in first.');
      return;
    }
    final header = cookies.map((cookie) => '${cookie.name}=${cookie.value}').join('; ');
    final session = MonitorSession(baseUri: widget.baseUri, cookieHeader: header);
    try {
      final response = await MonitorApi(session).loadSummary();
      if (!mounted) return;
      if (response.servers.isEmpty) {
        setState(() => _message = 'Session is valid. No SQL Server registrations are currently visible.');
      }
      Navigator.of(context).pop(session);
    } catch (_) {
      if (mounted) setState(() => _message = 'The session is not authorized for the DBA summary yet. Complete Monitor sign-in and try again.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final loginUri = widget.baseUri.resolve('/Account/Login');
    return Scaffold(
      appBar: AppBar(
        title: const Text('Monitor sign-in'),
        actions: [
          TextButton.icon(onPressed: _useSession, icon: const Icon(Icons.check_circle_outline), label: const Text('Use session')),
        ],
      ),
      body: Stack(
        children: [
          InAppWebView(
            initialUrlRequest: URLRequest(url: WebUri(loginUri.toString())),
            initialSettings: InAppWebViewSettings(javaScriptEnabled: true, thirdPartyCookiesEnabled: false),
            onLoadStart: (_, __) => setState(() => _loading = true),
            onLoadStop: (_, __) => setState(() => _loading = false),
          ),
          if (_loading) const LinearProgressIndicator(),
          if (_message != null)
            Align(
              alignment: Alignment.bottomCenter,
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                color: Colors.black87,
                child: Text(_message!, textAlign: TextAlign.center),
              ),
            ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: FilledButton.icon(onPressed: _useSession, icon: const Icon(Icons.dashboard_customize_outlined), label: const Text('Open DBA dashboard with this session')),
        ),
      ),
    );
  }
}

class MonitorApi {
  const MonitorApi(this.session);
  final MonitorSession session;

  Future<DbaSummary> loadSummary() async {
    final url = session.baseUri.resolve('/api/dba/summary');
    final response = await http.get(url, headers: {
      'Cookie': session.cookieHeader,
      'Accept': 'application/json',
      'X-Requested-With': 'XMLHttpRequest',
    }).timeout(const Duration(seconds: 15));
    if (response.statusCode != 200) throw StateError('Monitor returned HTTP ${response.statusCode}');
    return DbaSummary.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }
}

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key, required this.session});
  final MonitorSession session;

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  DbaSummary? _summary;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  Future<void> _refresh() async {
    setState(() { _loading = true; _error = null; });
    try {
      final summary = await MonitorApi(widget.session).loadSummary();
      if (mounted) setState(() => _summary = summary);
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _openFullDba() async {
    final uri = widget.session.baseUri.resolve('/dba');
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }

  @override
  Widget build(BuildContext context) {
    final summary = _summary;
    return Scaffold(
      appBar: AppBar(
        title: const Text('DBA Dashboard'),
        actions: [
          IconButton(tooltip: 'Refresh', onPressed: _loading ? null : _refresh, icon: const Icon(Icons.refresh_rounded)),
          IconButton(tooltip: 'Open full Monitor DBA', onPressed: _openFullDba, icon: const Icon(Icons.open_in_new_rounded)),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (_loading) const LinearProgressIndicator(),
            if (_error != null) _ErrorCard(message: _error!, onRetry: _refresh),
            if (summary != null) ...[
              Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  _KpiCard(label: 'SQL instances', value: '${summary.servers.length}', icon: Icons.dns_rounded),
                  _KpiCard(label: 'Critical / high', value: '${summary.criticalOrHigh}', icon: Icons.warning_amber_rounded),
                  _KpiCard(label: 'Query hotspots', value: '${summary.queryHotspots}', icon: Icons.query_stats_rounded),
                  _KpiCard(label: 'DBA to-do', value: '${summary.openTodos}', icon: Icons.checklist_rounded),
                ],
              ),
              const SizedBox(height: 18),
              Text('SQL instances', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 10),
              if (summary.servers.isEmpty)
                const Card(child: Padding(padding: EdgeInsets.all(20), child: Text('No SQL Server registrations are currently visible.'))),
              ...summary.servers.map((server) => Padding(padding: const EdgeInsets.only(bottom: 12), child: _ServerCard(server: server))),
              const SizedBox(height: 8),
              Text('DBA action list', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 10),
              ...summary.allTodos.map((todo) => Padding(padding: const EdgeInsets.only(bottom: 8), child: _TodoCard(todo: todo))),
              if (summary.allTodos.isEmpty)
                const Card(child: Padding(padding: EdgeInsets.all(20), child: Text('No DBA action is currently derived from the cached evidence.'))),
              const SizedBox(height: 18),
              Text('Updated ${summary.generatedAtUtc.toLocal()}', style: Theme.of(context).textTheme.bodySmall),
            ],
          ],
        ),
      ),
    );
  }
}

class _KpiCard extends StatelessWidget {
  const _KpiCard({required this.label, required this.value, required this.icon});
  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 168,
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Icon(icon),
            const SizedBox(height: 12),
            Text(value, style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700)),
            Text(label),
          ]),
        ),
      ),
    );
  }
}

class _ServerCard extends StatelessWidget {
  const _ServerCard({required this.server});
  final DbaServer server;

  @override
  Widget build(BuildContext context) {
    final memory = server.sqlMemoryPercent == null ? '—' : '${server.sqlMemoryPercent}%';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(server.name, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)), Text(server.endpoint, style: Theme.of(context).textTheme.bodySmall)])),
            _StatusPill(text: server.enabled ? 'Enabled' : 'Disabled', good: server.enabled),
          ]),
          const SizedBox(height: 14),
          Wrap(spacing: 18, runSpacing: 8, children: [
            Text('Memory $memory'),
            Text('DB ${server.databasesOnline ?? '—'}/${server.databasesTotal ?? '—'}'),
            Text('Blocked ${server.blockedRequests ?? '—'}'),
            Text('Runnable ${server.runnableTasks ?? '—'}'),
            Text('I/O pending ${server.pendingIoRequests ?? '—'}'),
            Text('Backup gaps ${server.fullBackupGaps}'),
          ]),
          if (server.recommendations.isNotEmpty) ...[
            const Divider(height: 24),
            ...server.recommendations.take(3).map((item) => Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const Icon(Icons.chevron_right_rounded, size: 18),
                const SizedBox(width: 4),
                Expanded(child: Text('${item.severity} · ${item.category} · ${item.title}')),
              ]),
            )),
          ],
        ]),
      ),
    );
  }
}

class _TodoCard extends StatelessWidget {
  const _TodoCard({required this.todo});
  final DbaTodo todo;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        leading: CircleAvatar(child: Text(todo.priority)),
        title: Text(todo.what),
        subtitle: Text('${todo.when}\n${todo.where}'),
        isThreeLine: true,
      ),
    );
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.text, required this.good});
  final String text;
  final bool good;
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
    decoration: BoxDecoration(color: good ? Colors.green.withValues(alpha: .15) : Colors.orange.withValues(alpha: .15), borderRadius: BorderRadius.circular(99)),
    child: Text(text),
  );
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const Text('Dashboard unavailable', style: TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: 6),
        Text(message),
        const SizedBox(height: 10),
        OutlinedButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh_rounded), label: const Text('Retry')),
      ]),
    ),
  );
}

class DbaSummary {
  const DbaSummary({required this.generatedAtUtc, required this.criticalOrHigh, required this.queryHotspots, required this.openTodos, required this.servers});
  final DateTime generatedAtUtc;
  final int criticalOrHigh;
  final int queryHotspots;
  final int openTodos;
  final List<DbaServer> servers;

  List<DbaTodo> get allTodos => servers.expand((server) => server.todos).toList()
    ..sort((a, b) => a.priority.compareTo(b.priority));

  factory DbaSummary.fromJson(Map<String, dynamic> json) => DbaSummary(
    generatedAtUtc: DateTime.tryParse('${json['generatedAtUtc']}') ?? DateTime.now().toUtc(),
    criticalOrHigh: _asInt(json['criticalOrHighRecommendations']),
    queryHotspots: _asInt(json['queryHotspots']),
    openTodos: _asInt(json['openTodoItems']),
    servers: ((json['servers'] as List?) ?? const []).whereType<Map<String, dynamic>>().map(DbaServer.fromJson).toList(),
  );
}

class DbaServer {
  const DbaServer({required this.name, required this.endpoint, required this.enabled, required this.databasesOnline, required this.databasesTotal, required this.sqlMemoryPercent, required this.blockedRequests, required this.runnableTasks, required this.pendingIoRequests, required this.fullBackupGaps, required this.recommendations, required this.todos});
  final String name;
  final String endpoint;
  final bool enabled;
  final int? databasesOnline;
  final int? databasesTotal;
  final int? sqlMemoryPercent;
  final int? blockedRequests;
  final int? runnableTasks;
  final int? pendingIoRequests;
  final int fullBackupGaps;
  final List<DbaRecommendationItem> recommendations;
  final List<DbaTodo> todos;

  factory DbaServer.fromJson(Map<String, dynamic> json) => DbaServer(
    name: '${json['name'] ?? 'Unnamed server'}', endpoint: '${json['endpoint'] ?? ''}', enabled: json['enabled'] == true,
    databasesOnline: _nullableInt(json['databasesOnline']), databasesTotal: _nullableInt(json['databasesTotal']), sqlMemoryPercent: _nullableInt(json['sqlMemoryPercent']),
    blockedRequests: _nullableInt(json['blockedRequests']), runnableTasks: _nullableInt(json['runnableTasks']), pendingIoRequests: _nullableInt(json['pendingIoRequests']),
    fullBackupGaps: _asInt(json['fullBackupGaps']),
    recommendations: ((json['recommendations'] as List?) ?? const []).whereType<Map<String, dynamic>>().map(DbaRecommendationItem.fromJson).toList(),
    todos: ((json['todos'] as List?) ?? const []).whereType<Map<String, dynamic>>().map(DbaTodo.fromJson).toList(),
  );
}

class DbaRecommendationItem {
  const DbaRecommendationItem(this.severity, this.category, this.title);
  final String severity;
  final String category;
  final String title;
  factory DbaRecommendationItem.fromJson(Map<String, dynamic> json) => DbaRecommendationItem('${json['severity'] ?? ''}', '${json['category'] ?? ''}', '${json['title'] ?? ''}');
}

class DbaTodo {
  const DbaTodo(this.priority, this.what, this.when, this.where);
  final String priority;
  final String what;
  final String when;
  final String where;
  factory DbaTodo.fromJson(Map<String, dynamic> json) => DbaTodo('${json['priority'] ?? ''}', '${json['what'] ?? ''}', '${json['when'] ?? ''}', '${json['where'] ?? ''}');
}

int _asInt(dynamic value) => value is num ? value.toInt() : int.tryParse('$value') ?? 0;
int? _nullableInt(dynamic value) => value == null ? null : _asInt(value);
