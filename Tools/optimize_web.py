"""Apply repeatable mobile fixes to a Unity Web output, without rebuilding wasm."""
import gzip
import pathlib
import sys

root = pathlib.Path(sys.argv[1]).resolve()
p = root / 'index.html'
s = p.read_text()
start = '<!-- angel-mobile-performance:start -->'
end = '<!-- angel-mobile-performance:end -->'
if start in s:
    a = s.index(start)
    b = s.index(end, a) + len(end)
    s = s[:a] + s[b:]
if True:
    # Run before the Unity loader script. Retain CSS sizing and Unity input coordinates.
    patch = '''<!-- angel-mobile-performance:start -->
    <script>
    const angelMobile = matchMedia('(pointer: coarse)').matches || /iPhone|iPad|iPod|Android/i.test(navigator.userAgent);
    // Cap total render pixels as well as DPR (landscape tablets can be very wide).
    const angelDpr = () => Math.min(1, 960 / Math.max(innerWidth, innerHeight));
    window.angelPrepare = async function(config) {
      config.devicePixelRatio = angelMobile ? angelDpr() : Math.min(devicePixelRatio, 1.5);
      config.productVersion = '1.0-mobile-2';
      const urls = [];
      window.angelBlobUrls = urls;
      if (typeof DecompressionStream === 'undefined') return;
      await Promise.all(['dataUrl', 'codeUrl'].map(async key => {
        const original = config[key];
        try {
          const response = await fetch(original + '.gz?v=mobile2');
          if (!response.ok || !response.body) throw new Error('Compressed download unavailable');
          const stream = response.body.pipeThrough(new DecompressionStream('gzip'));
          const blob = new Blob([await new Response(stream).arrayBuffer()], {
            type: key === 'codeUrl' ? 'application/wasm' : 'application/octet-stream'
          });
          config[key] = URL.createObjectURL(blob);
          urls.push(config[key]);
        } catch (error) {
          // Original binaries remain available for older browsers or network failures.
          console.warn('Using original Unity asset:', key, error);
          config[key] = original;
        }
      }));
    };
    </script>
    <!-- angel-mobile-performance:end -->'''
    s = s.replace('    <script>\n      var canvas', '    ' + patch + '\n    <script>\n      var canvas', 1)
    s = s.replace('script.onload = () => {', 'script.onload = async () => {\n        await window.angelPrepare(config);', 1)
    if 'window.angelBlobUrls || []' not in s:
        s = s.replace('document.querySelector("#unity-loading-bar").style.display = "none";', '''(window.angelBlobUrls || []).forEach(url => URL.revokeObjectURL(url));
                window.angelBlobUrls = [];
                document.querySelector("#unity-loading-bar").style.display = "none";''', 1)
    p.write_text(s)
for name in ('Web.data', 'Web.wasm'):
    source = root / 'Build' / name
    data = source.read_bytes()
    packed = gzip.compress(data, compresslevel=9, mtime=0)
    source.with_name(name + '.gz').write_bytes(packed)
    assert gzip.decompress(packed) == data
    print(f'{name}: {len(data):,} -> {len(packed):,} bytes ({100*(1-len(packed)/len(data)):.1f}% smaller)')
assert 'await window.angelPrepare(config)' in p.read_text()
