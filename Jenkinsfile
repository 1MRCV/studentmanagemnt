pipeline {

    agent { label 'windows-agent' }

    // ---------------- PARAMETERS ----------------
    properties([
        parameters([

            // Environment selection
            choice(
                name: 'ENVIRONMENT',
                choices: ['DEV','PRODUCTION'],
                description: 'Select deployment environment'
            ),

            // Deployment action: deploy new or rollback
            choice(
                name: 'ACTION',
                choices: ['DEPLOY','ROLLBACK'],
                description: 'Select deployment action'
            ),

            // Artifact dropdown using Active Choices Plugin
            [$class: 'CascadeChoiceParameter',
             name: 'ARTIFACT_BUILD',
             description: 'Select artifact version',
             choiceType: 'PT_SINGLE_SELECT',
             referencedParameters: 'ENVIRONMENT',
             script: [
                 $class: 'GroovyScript',
                 script: [
                     script: '''
                         def root = "C:/jenkins-artifacts/" + ENVIRONMENT
                         def dir = new File(root)
                         if(!dir.exists()) return ["No artifacts found"]
                         return dir.list().sort().reverse()
                     ''',
                     sandbox: true
                 ]
             ]
            ],

            // Git branch to build
            string(
                name: 'BRANCH',
                defaultValue: 'main',
                description: 'Git branch to build'
            )
        ])
    ])

    // ---------------- ENVIRONMENT VARIABLES ----------------
    environment {
        GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"
        PUBLISH_FOLDER = "publish"
        ARTIFACT_ROOT = "C:\\jenkins-artifacts"
        DEV_ARTIFACT = "C:\\jenkins-artifacts\\DEV"
        PROD_ARTIFACT = "C:\\jenkins-artifacts\\PROD"
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

        // ---------------- Set Build Name ----------------
        stage('Set Build Name') {
            steps {
                script {
                    def date = new Date().format("yyyy-MM-dd")
                    currentBuild.displayName = "#${env.BUILD_NUMBER} - ${params.ENVIRONMENT} - ${date}"
                }
            }
        }

        // ---------------- Clean Workspace ----------------
        stage('Clean Workspace') {
            steps { deleteDir() }
        }

        // ---------------- Checkout Code ----------------
        stage('Checkout Code') {
            steps {
                git branch: "${params.BRANCH}", url: "${env.GIT_REPO}"
            }
        }

        // ---------------- Restore Dependencies ----------------
        stage('Restore Dependencies') {
            steps { powershell 'dotnet restore' }
        }

        // ---------------- Build Application ----------------
        stage('Build Application') {
            when { expression { params.ACTION == 'DEPLOY' && params.ENVIRONMENT == 'DEV' } }
            steps { powershell 'dotnet build --configuration Release' }
        }

        // ---------------- Publish Website ----------------
        stage('Publish Website') {
            when { expression { params.ACTION == 'DEPLOY' && params.ENVIRONMENT == 'DEV' } }
            steps { powershell 'dotnet publish -c Release -o $env:PUBLISH_FOLDER' }
        }

        // ---------------- Create Artifact Version ----------------
        stage('Create Artifact Version') {
            when { expression { params.ACTION == 'DEPLOY' && params.ENVIRONMENT == 'DEV' } }
            steps {
                powershell '''
                $build = $env:BUILD_NUMBER
                $date = Get-Date -Format "yyyy-MM-dd"
                $dateTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                $version = "1.0.$build"
                $commit = git rev-parse --short HEAD
                $branch = git rev-parse --abbrev-ref HEAD

                $artifactRoot = "$env:DEV_ARTIFACT"
                if (!(Test-Path $artifactRoot)) { New-Item -ItemType Directory -Path $artifactRoot }

                $buildFolder = "$artifactRoot\\build_${build}_$date"
                if (!(Test-Path $buildFolder)) { New-Item -ItemType Directory -Path $buildFolder }

                $zip = "$buildFolder\\artifact.zip"
                Compress-Archive -Path "$env:PUBLISH_FOLDER\\*" -DestinationPath $zip -Force

                $info = @"
Application: StudentPortal
Environment: DEV
Version: $version
Build Number: $build
Branch: $branch
Commit: $commit
Build Date: $dateTime
"@
                $info | Out-File "$buildFolder\\build-info.txt"
                Write-Host "Artifact stored at $buildFolder"
                '''
            }
        }

        // ---------------- Promote DEV → PROD ----------------
        stage('Promote DEV Artifact to PROD') {
            when { expression { params.ENVIRONMENT == 'PRODUCTION' && params.ACTION == 'DEPLOY' } }
            steps {
                powershell '''
                $devRoot = "C:\\jenkins-artifacts\\DEV"
                $prodRoot = "C:\\jenkins-artifacts\\PROD"
                $artifact = "$devRoot\\$env:ARTIFACT_BUILD"
                if(!(Test-Path $artifact)){ throw "DEV artifact not found" }
                $target = "$prodRoot\\$env:ARTIFACT_BUILD"
                Copy-Item $artifact -Destination $target -Recurse -Force
                Write-Host "Artifact promoted from DEV to PROD"
                '''
            }
        }

        // ---------------- Deploy Artifact ----------------
        stage('Deploy Artifact') {
            steps {
                powershell '''
                Import-Module WebAdministration

                $envName = "$env:ENVIRONMENT"
                $artifactBuild = "$env:ARTIFACT_BUILD"

                if($envName -eq "DEV"){
                    $artifactRoot="$env:DEV_ARTIFACT"
                    $siteName="$env:DEV_SITE"
                    $pool="$env:DEV_POOL"
                    $path="$env:DEV_PATH"
                    $port="$env:DEV_PORT"
                } else {
                    $artifactRoot="$env:PROD_ARTIFACT"
                    $siteName="$env:PROD_SITE"
                    $pool="$env:PROD_POOL"
                    $path="$env:PROD_PATH"
                    $port="$env:PROD_PORT"
                }

                $folder = Get-ChildItem $artifactRoot | Where-Object {$_.Name -eq $artifactBuild} | Select-Object -First 1
                if(!$folder){ throw "Artifact not found: $artifactBuild" }

                $zipPath = "$($folder.FullName)\\artifact.zip"
                Write-Host "Deploying artifact $artifactBuild to $envName"

                if (!(Test-Path "IIS:\\AppPools\\$pool")){ New-WebAppPool -Name $pool }
                if (!(Test-Path $path)){ New-Item -ItemType Directory -Path $path }
                if (!(Test-Path "IIS:\\Sites\\$siteName")){ New-Website -Name $siteName -Port $port -PhysicalPath $path -ApplicationPool $pool }

                $state = (Get-WebAppPoolState -Name $pool).Value
                if ($state -eq "Started"){ Stop-WebAppPool $pool; Start-Sleep -Seconds 2 }

                Remove-Item "$path\\*" -Recurse -Force -ErrorAction SilentlyContinue
                Expand-Archive -Path $zipPath -DestinationPath $path -Force

                $versionFile = "$path\\version.txt"
                "Environment: $envName" | Out-File $versionFile
                "Build: $artifactBuild" | Out-File $versionFile -Append
                "Date: $(Get-Date)" | Out-File $versionFile -Append

                Start-WebAppPool $pool
                Start-Website $siteName
                Write-Host "Deployment completed successfully"
                '''
            }
        }

        // ---------------- Verify Deployment ----------------
        stage('Verify Deployment') {
            steps {
                powershell '''
                Import-Module WebAdministration
                $pool = $env:ENVIRONMENT -eq "DEV" ? $env:DEV_POOL : $env:PROD_POOL
                $state = (Get-WebAppPoolState -Name $pool).Value
                Write-Host "App Pool State: $state"
                if($state -ne "Started"){ throw "Deployment verification failed" }
                Write-Host "Deployment verified successfully"
                '''
            }
        }
    }

    post {
        success { echo "Build ${BUILD_NUMBER} deployed successfully." }
        failure { echo "Deployment failed. Check logs." }
    }
}
