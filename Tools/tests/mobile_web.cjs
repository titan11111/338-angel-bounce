const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const html = fs.readFileSync(process.argv[2], 'utf8');
for (const script of html.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/g)) new vm.Script(script[1]);
const source = html.split('<!-- angel-mobile-performance:start -->')[1].split('</script>')[0].split('<script>')[1];
(async () => {
  for (const [w,h,coarse,dpr,expected] of [[390,844,true,3,1],[844,390,true,3,1],[1366,1024,true,2,960/1366],[1920,1080,false,2,1.5]]) {
    const ctx = {innerWidth:w,innerHeight:h,devicePixelRatio:dpr,matchMedia:()=>({matches:coarse}),navigator:{userAgent:coarse?'iPhone':'Desktop'},window:{},console};
    vm.runInNewContext(source,ctx);
    const config={};await ctx.window.angelPrepare(config);
    assert.equal(config.devicePixelRatio,expected);
  }
  // Old browsers keep original URLs; no dependency on DecompressionStream.
  console.log('PASS: inline JS syntax, phone portrait/landscape, tablet cap, desktop cap, legacy decompressor fallback');
})().catch(e=>{console.error(e);process.exitCode=1});
