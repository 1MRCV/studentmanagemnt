properties([
parameters([

choice(
name: 'ENVIRONMENT',
choices: ['DEV','PRODUCTION'],
description: 'Select environment'
),

choice(
name: 'ACTION',
choices: ['DEPLOY','ROLLBACK'],
description: 'Deployment action'
),

string(
name: 'BRANCH',
defaultValue: 'main',
description: 'Git branch to build (used only for DEV)'
),

[$class: 'CascadeChoiceParameter',
name: 'ARTIFACT_BUILD',
description: 'Select artifact build',
choiceType: 'PT_SINGLE_SELECT',
filterable: true,
script: [
$class: 'GroovyScript',
script: [
$class: 'SecureGroovyScript',
sandbox: false,
script: '''

def path = "C:/jenkins-artifacts"

def dir = new File(path)

if(!dir.exists()){
return ["No Artifacts Found"]
}

def folders = dir.listFiles()
.findAll { it.isDirectory() }
.sort { -it.lastModified() }
.collect { it.name }

return folders

'''
]
]
]

])
])

pipeline {

agent { label 'windows-agent' }

environment {

GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"

ARTIFACT_ROOT = "C:\\jenkins-artifacts"

DEV_PATH = "C:\\inetpub\\dev"
PROD_PATH = "C:\\inetpub\\prod"

DEV_SITE = "student-dev"
PROD_SITE = "student-prod"

DEV_POOL = "student-dev-pool"
PROD_POOL = "student-prod-pool"

DEV_PORT = "8081"
PROD_PORT = "8082"

}

stages {

stage('Set Build Name') {
steps {
script {

def date = new Date().format("yyyy-MM-dd")

currentBuild.displayName =
"#${env.BUILD_NUMBER} - ${params.ENVIRONMENT} - ${date}"

}
}
}

stage('Clean Workspace') {
steps {
deleteDir()
}
}

stage('Checkout Code') {

when {
expression { params.ENVIRONMENT == 'DEV' }
}

steps {

git branch: "${params.BRANCH}",
url: "${env.GIT_REPO}"

}
}

stage('Build Artifact') {

when {
expression { params.ENVIRONMENT == 'DEV' }
}

steps {

powershell 'dotnet restore'
powershell 'dotnet build --configuration Release'
powershell 'dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish'

powershell '''

$build = $env:BUILD_NUMBER
$date = Get-Date -Format "yyyy-MM-dd"
$dateTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

$artifactRoot = "C:\\jenkins-artifacts"

if (!(Test-Path $artifactRoot)) {
New-Item -ItemType Directory -Path $artifactRoot
}

$buildFolder = "$artifactRoot\\build_${build}_$date"

New-Item -ItemType Directory -Path $buildFolder -Force

Compress-Archive `
-Path "publish\\*" `
-DestinationPath "$buildFolder\\artifact.zip" `
-Force

$commit = git rev-parse --short HEAD
$branch = git rev-parse --abbrev-ref HEAD

$info = @"
Application: StudentPortal
Environment: DEV
Build Number: $build
Branch: $branch
Commit: $commit
Build Date: $dateTime
"@

$info | Out-File "$buildFolder\\build-info.txt"

Write-Host "Artifact stored in $buildFolder"

'''
}
}

stage('Deploy') {

when {
expression { params.ACTION == 'DEPLOY' }
}

steps {

powershell """

Import-Module WebAdministration

\$envName = "${params.ENVIRONMENT}"
\$artifactBuild = "${params.ARTIFACT_BUILD}"
\$artifactRoot = "${env.ARTIFACT_ROOT}"

if([string]::IsNullOrEmpty(\$artifactBuild)){

\$latest = Get-ChildItem \$artifactRoot |
Where-Object {\$_.PSIsContainer} |
Sort-Object LastWriteTime -Descending |
Select-Object -First 1

if(\$latest -eq \$null){
Write-Error "No artifacts found"
exit 1
}

\$artifactBuild = \$latest.Name

}

\$folder = Join-Path \$artifactRoot \$artifactBuild
\$zipPath = "\$folder\\artifact.zip"

if(\$envName -eq "DEV"){

\$site="${env.DEV_SITE}"
\$pool="${env.DEV_POOL}"
\$path="${env.DEV_PATH}"
\$port="${env.DEV_PORT}"

}
else{

\$site="${env.PROD_SITE}"
\$pool="${env.PROD_POOL}"
\$path="${env.PROD_PATH}"
\$port="${env.PROD_PORT}"

}

if (!(Test-Path "IIS:\\AppPools\\\$pool")) {
New-WebAppPool -Name \$pool
}

if (!(Test-Path "IIS:\\Sites\\\$site")) {

New-Website `
-Name \$site `
-Port \$port `
-PhysicalPath \$path `
-ApplicationPool \$pool

}

Stop-WebAppPool \$pool -ErrorAction SilentlyContinue

Remove-Item "\$path\\*" -Recurse -Force -ErrorAction SilentlyContinue

Expand-Archive `
-Path \$zipPath `
-DestinationPath \$path `
-Force

Start-WebAppPool \$pool
Start-Website \$site

Write-Host "Deployment Completed"

"""
}
}

stage('Verify Deployment') {

steps {

powershell '''
Import-Module WebAdministration
Write-Host "Deployment successful"
Get-Website
'''

}

}

}

}
