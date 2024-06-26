# SecureWss
Testing WSS on 4-Series Processor
the C5Debugger.dll reference will need to be manually added in order to compile.

Project works from web browsers as well as TSW's now.

To get to work on TSW, you will need to download the certificate from `/user` and upload it to the TSW's you want to use it on into the `/User/Cert`directory then console into the TSW and type `certif addf {certname} root` then load your UI and it should be able to connect.

# Additional to JayLia base program:
- Bouncy certificate now generates a root certificate (exp. 50 years) and then a server certificate (exp. 1 year) that is signed by this root certificate.
- Bouncy certificate checks every 12 hours for whether the server certificate exists. If it does exist it will check whether it expires within 3 days. If it does expire within 3 days it will generate a new server certificate.
- If the user browses to the URL of the http server and then appends it by /file/rootCert it will download the root certificate file to the user's device.
- The front end can get the system.json file by using the URL of the http server appended by /file/system
- Additional code added to instantiate user interfaces using reflection based on the system.json file
