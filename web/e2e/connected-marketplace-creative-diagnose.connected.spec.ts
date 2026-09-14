import { expect, test, type Page } from '@playwright/test'
const buyerTenantId='10000000-0000-0000-0000-000000000040'
const supplierTenantId='10000000-0000-0000-0000-000000000002'
const buyerUserId='10000000-0000-0000-0000-000000000001'
const supplierUserId='10000000-0000-0000-0000-000000000041'
type Session={antiforgeryToken:string}
test('diagnose creative ownership and reviews',async({page})=>{
 await signIn(page); await bootstrap(page); await switchIdentity(page,buyerUserId); await chooseWorkspace(page,buyerTenantId)
 const campaigns=await get(page,buyerTenantId,'campaigns') as any[]; const campaign=campaigns.sort((a,b)=>String(b.createdAtUtc).localeCompare(String(a.createdAtUtc)))[0]
 const detail=await get(page,buyerTenantId,`campaigns/${campaign.id}`) as any
 console.log('buyer-campaign',JSON.stringify({id:detail.id,status:detail.status,version:detail.version,creative:detail.creative},null,2))
 await switchIdentity(page,supplierUserId); await chooseWorkspace(page,supplierTenantId)
 for(const req of detail.creative?.requirements??[]){ if(!req.asset) continue; const asset=await get(page,supplierTenantId,`creative-assets/${req.asset.id}`); console.log('supplier-asset',JSON.stringify({requirementSupplier:req.supplierTenantId,asset},null,2)) }
})
async function get(page:Page,tenant:string,path:string){const r=await page.request.get(`/api/v1/tenants/${tenant}/${path}`);expect(r.ok(),await r.text()).toBe(true);return await r.json()}
async function signIn(page:Page){await page.goto('/sign-in');await page.getByRole('button',{name:/Continue to Advertified/}).click();await page.getByRole('button',{name:/Advertified Local/}).click()}
async function bootstrap(page:Page){const s=await session(page);const r=await page.request.post('/api/v1/development/connected-acceptance/bootstrap',{data:{},headers:h(s.antiforgeryToken)});expect(r.status(),await r.text()).toBe(200)}
async function switchIdentity(page:Page,userId:string){const s=await session(page);const r=await page.request.post('/api/v1/development/connected-acceptance/identity',{data:{userId},headers:h(s.antiforgeryToken)});expect(r.status(),await r.text()).toBe(200)}
async function session(page:Page){const r=await page.request.get('/api/v1/session');expect(r.ok(),await r.text()).toBe(true);return await r.json() as Session}
async function chooseWorkspace(page:Page,id:string){await page.evaluate((tenantId)=>sessionStorage.setItem('advertified.workspace',JSON.stringify({tenantId})),id)}
function h(t:string){return{Origin:'http://localhost:3017','X-CSRF-TOKEN':t,'Idempotency-Key':crypto.randomUUID(),'X-Correlation-ID':crypto.randomUUID()}}
