import json,uuid,secrets,http.cookiejar
from urllib.request import Request,build_opener,HTTPCookieProcessor
from urllib.error import HTTPError
from datetime import datetime,timedelta
from zoneinfo import ZoneInfo
from concurrent.futures import ThreadPoolExecutor
import os
base=os.environ.get('BOOKING_TEST_API', 'http://localhost:5130/api')
class Client:
 def __init__(self):
  self.cookies=http.cookiejar.CookieJar();self.opener=build_opener(HTTPCookieProcessor(self.cookies))
 def call(self,path,method='GET',data=None,headers=None,csrf=True):
  h={'Content-Type':'application/json',**(headers or {})}
  if method!='GET' and csrf:h['X-CSRF-TOKEN']=self.call('/auth/csrf')[1]['token']
  req=Request(base+path,method=method,data=json.dumps(data).encode() if data is not None else None,headers=h)
  try:
   with self.opener.open(req,timeout=30) as r:
    raw=r.read();return r.status,json.loads(raw) if raw else None
  except HTTPError as e:
   raw=e.read()
   try:body=json.loads(raw)
   except:body=None
   return e.code,body
anon=Client();status,_=anon.call('/bookings');assert status==401,('anonymous',status)
assert anon.call('/auth/register','POST',{'email':'blocked@example.com','password':'ExamplePassword!123'},csrf=False)[0]==400
print('Anonymous private requests: 401; registration without CSRF: 400',flush=True)
clients=[Client(),Client()];users=[]
for client in clients:
 email='identity-test-'+uuid.uuid4().hex+'@example.com';password='Test!Aa1'+secrets.token_hex(12)
 credentials={'email':email,'password':password}
 status,_=client.call('/auth/register','POST',credentials);assert status==201,('register',status)
 old_token=client.call('/auth/csrf')[1]['token']
 status,body=client.call('/auth/login','POST',credentials);assert status==200,('login',status)
 users.append(body['user'])
 assert client.call('/auth/me')[1]['user']['id']==body['user']['id']
 assert client.call('/auth/logout','POST',headers={'X-CSRF-TOKEN':old_token},csrf=False)[0]==400
 auth_cookie=next(c for c in client.cookies if c.name=='BookingSystem.Auth')
 assert any(key.lower() == 'httponly' for key in auth_cookie._rest)
 assert client.call('/providers','POST',{'name':'Unauthorized Writer'})[0]==403
 print('Registration/login, HttpOnly session, stale CSRF rejection, management denied: OK',flush=True)
a,b=clients
assert a.call('/bookings/1')[0]==404
assert a.call('/bookings/1/cancel','POST')[0]==404
print('Legacy booking without owner inaccessible: 404',flush=True)
_,services=a.call('/services');service=next(s for s in services if s['providerId']==1)
day=datetime.now(ZoneInfo('Europe/Warsaw')).date()+timedelta(days=7)
while day.weekday()!=0:day+=timedelta(days=1)
_,slots=a.call(f'/services/{service["id"]}/available-slots?date={day}')
payload={'serviceId':service['id'],'startTime':slots[0]['startTime'],'customerName':'Identity Test','customerEmail':'identity-customer@example.com','userId':users[1]['id']}
key=str(uuid.uuid4());headers={'Idempotency-Key':key}
with ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(lambda _:a.call('/bookings','POST',payload,headers),range(2)))
assert sorted(s for s,_ in results)==[200,201],[(s,body.get('code') if isinstance(body,dict) else None) for s,body in results]
bid=results[0][1]['id'];assert all(body['id']==bid for _,body in results)
assert a.call('/bookings','POST',{**payload,'customerName':'Changed'},headers)[0]==409
assert a.call('/bookings/'+str(bid))[0]==200
assert b.call('/bookings/'+str(bid))[0]==404
assert b.call('/bookings/'+str(bid)+'/cancel','POST')[0]==404
assert any(item['id']==bid for item in a.call('/bookings')[1])
assert not any(item['id']==bid for item in b.call('/bookings')[1])
assert a.call('/bookings/'+str(bid)+'/cancel','POST',csrf=False)[0]==400
assert b.call('/bookings','POST',payload,headers)[0]==409
print('Forged userId ignored; concurrent replay safe; other account cannot read/cancel/list/replay booking',flush=True)
payload2={**payload,'startTime':slots[4]['startTime']}
status,booking2=b.call('/bookings','POST',payload2,headers);assert status==201,('second account booking',status)
assert a.call('/bookings/'+str(booking2['id']))[0]==404
print('Idempotency key scoped to account: same key creates a separate own booking',flush=True)
assert a.call('/bookings/'+str(bid)+'/cancel','POST')[0]==204
assert b.call('/bookings/'+str(booking2['id'])+'/cancel','POST')[0]==204
race_payload={**payload,'startTime':slots[8]['startTime']}
for attempt in range(5):
 with ThreadPoolExecutor(max_workers=2) as pool:
  race_results=list(pool.map(lambda client:client.call('/bookings','POST',race_payload,{'Idempotency-Key':str(uuid.uuid4())}),clients))
 assert sorted(status for status,_ in race_results)==[201,409],[(status,body.get('code') if isinstance(body,dict) else None) for status,body in race_results]
 for client,(status,body) in zip(clients,race_results):
  if status==201:assert client.call('/bookings/'+str(body['id'])+'/cancel','POST')[0]==204
print('Five independent booking races: each returned one 201 and one 409, without server errors',flush=True)
# Public registration must never accept a role from a request payload.
import subprocess,shutil
from pathlib import Path
admin=Client();admin_email='admin-test-'+uuid.uuid4().hex+'@example.com'
admin_credentials={'email':admin_email,'password':'Test!Aa1'+secrets.token_hex(12),'isAdministrator':True,'roles':['Administrator']}
assert admin.call('/auth/register','POST',admin_credentials)[0]==201
assert admin.call('/auth/login','POST',admin_credentials)[1]['user']['isAdministrator'] is False
assert admin.call(f'/admin/bookings?date={day}')[0]==403
assert anon.call(f'/admin/bookings?date={day}')[0]==401
dotnet=shutil.which('dotnet') or '/usr/local/share/dotnet/dotnet'
root=Path(__file__).resolve().parents[2]
for _ in range(2):
 provision=subprocess.run([dotnet,'run','--project',str(root/'backend'),'--no-build','--no-launch-profile','--','--grant-admin',admin_email],capture_output=True,text=True,timeout=60)
 assert provision.returncode==0,('admin provisioning failed',provision.returncode)
# Existing cookies keep their original claims until a new sign-in.
assert admin.call('/auth/me')[1]['user']['isAdministrator'] is False
assert admin.call('/auth/logout','POST')[0]==204
assert admin.call('/auth/login','POST',admin_credentials)[1]['user']['isAdministrator'] is True
assert admin.call('/auth/me')[1]['user']['isAdministrator'] is True
assert a.call('/auth/me')[1]['user']['isAdministrator'] is False
for path,method,data in [('/services','POST',{'providerId':1,'name':'Denied','durationMinutes':30,'price':60}),('/providers/1/working-hours/1','PUT',{'startTime':'09:00','endTime':'17:00'}),('/providers/1/working-hours/1','DELETE',None),('/admin/bookings/1/cancel','POST',None)]:
 assert a.call(path,method,data)[0]==403,(path,'normal user must be denied')
assert admin.call('/providers','POST',{'name':'CSRF blocked'},csrf=False)[0]==400
assert admin.call('/providers','POST',{'name':'  '})[0]==400
status,provider=admin.call('/providers','POST',{'name':'Admin Test '+uuid.uuid4().hex[:8]});assert status==201
pid=provider['id']
assert admin.call('/services','POST',{'providerId':pid,'name':'Test service','durationMinutes':0,'price':0})[0]==400
status,new_service=admin.call('/services','POST',{'providerId':pid,'name':'Test service','durationMinutes':30,'price':50});assert status==201
hours_path=f'/providers/{pid}/working-hours/1'
assert admin.call(hours_path,'PUT',{'startTime':'17:00','endTime':'09:00'})[0]==400
assert admin.call(hours_path,'PUT',{'startTime':'09:00','endTime':'17:00'})[0]==204
assert len(admin.call(f'/providers/{pid}/working-hours')[1])==1
assert admin.call(hours_path,'PUT',{'startTime':'10:00','endTime':'16:00'})[0]==204
assert len(admin.call(f'/providers/{pid}/working-hours')[1])==1
_,new_slots=a.call(f'/services/{new_service["id"]}/available-slots?date={day}')
assert len(new_slots)>0
status,admin_booking=a.call('/bookings','POST',{'serviceId':new_service['id'],'startTime':new_slots[0]['startTime'],'customerName':'Admin Visibility Test','customerEmail':'admin-visibility@example.com'},{'Idempotency-Key':str(uuid.uuid4())});assert status==201
aid=admin_booking['id']
assert admin.call('/admin/bookings')[0]==400
assert admin.call('/admin/bookings?date=bad')[0]==400
assert admin.call('/admin/bookings?date=0001-01-01')[0]==400
assert admin.call('/admin/bookings?date=9999-12-31')[0]==400
assert admin.call(f'/admin/bookings?date={day}&providerId=-1')[0]==400
status,listing=admin.call(f'/admin/bookings?date={day}&providerId={pid}');assert status==200
assert listing['truncated'] is False and len(listing['bookings'])==1
assert listing['bookings'][0]['id']==aid and listing['bookings'][0]['serviceName']=='Test service'
assert not any(row['id']==aid for row in admin.call(f'/admin/bookings?date={day}&providerId=1')[1]['bookings'])
assert not any(row['id']==aid for row in admin.call(f'/admin/bookings?date={day+timedelta(days=1)}')[1]['bookings'])
assert admin.call(f'/bookings/{aid}')[0]==404 # Customer route retains ownership rules even for admin.
assert admin.call(f'/admin/bookings/{aid}/cancel','POST',csrf=False)[0]==400
assert admin.call(hours_path,'DELETE')[0]==204
assert a.call(f'/services/{new_service["id"]}/available-slots?date={day}')[1]==[]
assert a.call(f'/bookings/{aid}')[1]['status']==0 # Closing a day preserves existing appointments.
assert admin.call(f'/admin/bookings/{aid}/cancel','POST')[0]==204
assert admin.call(f'/admin/bookings/{aid}/cancel','POST')[0]==204
assert admin.call('/admin/bookings/2147483647/cancel','POST')[0]==404
assert a.call(f'/bookings/{aid}')[1]['status']==1
assert admin.call('/auth/logout','POST')[0]==204
assert admin.call(f'/admin/bookings?date={day}')[0]==401
print('Administrator CLI, refreshed role claims, role denial, CSRF, management, day filters and cancellation: OK',flush=True)

assert a.call('/auth/logout','POST')[0]==204
assert a.call('/bookings')[0]==401
print('Owner cancellations: 204; logout removes private access; test bookings cancelled',flush=True)
