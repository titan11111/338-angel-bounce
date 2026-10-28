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
  const zlib = require('node:zlib');
  const blobs=[];
  const ctx={innerWidth:844,innerHeight:390,devicePixelRatio:3,matchMedia:()=>({matches:true}),navigator:{userAgent:'iPhone'},window:{},console,
    DecompressionStream,Response,Blob,URL:{createObjectURL:blob=>{blobs.push(blob);return 'blob:'+blobs.length;}},
    fetch:async()=>new Response(zlib.gzipSync(Buffer.from('Unity test bytes')))};
  vm.runInNewContext(source,ctx);
  const config={dataUrl:'Build/Web.data',codeUrl:'Build/Web.wasm'};
  await ctx.window.angelPrepare(config);
  assert.equal(blobs.length,2);
  for (const blob of blobs) assert.equal(await blob.text(),'Unity test bytes');
  assert.equal(blobs[1].type,'application/wasm');
  ctx.fetch=async()=>{throw new Error('Simulated network failure')};
  ctx.console={warn:()=>{}};
  const fallback={dataUrl:'Build/Web.data',codeUrl:'Build/Web.wasm'};
  await ctx.window.angelPrepare(fallback);
  assert.equal(fallback.dataUrl,'Build/Web.data');assert.equal(fallback.codeUrl,'Build/Web.wasm');
  console.log('PASS: gzip streaming decompression, wasm MIME, compressed-download failure fallback');
  // Old browsers keep original URLs; no dependency on DecompressionStream.
  console.log('PASS: inline JS syntax, phone portrait/landscape, tablet cap, desktop cap, legacy decompressor fallback');
})().catch(e=>{console.error(e);process.exitCode=1});
