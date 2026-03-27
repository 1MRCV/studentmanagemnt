pipeline {

  agent { label 'windows-agent' }

  options {
    skipDefaultCheckout(true)   // 🔥 IMPORTANT FIX
  }

  parameters {

    choice(
      name: 'ENVIRONMENT',
      choices: ['DEV', 'PRODUCTION'],
      description: 'Select environment'
    )

    choice(
      name: 'ACTION',
      choices: ['DEPLOY', 'ROLLBACK'],
      description: 'Deploy or rollback'
    )

    string(
      name: 'BRANCH',
      defaultValue: 'main',
      description: 'Git branch (DEV only)'
    )

    string(
      name: 'ARTIFACT_BUILD',
      defaultValue: '',
      description: 'Build number for PROD (e.g., Build_17)'
    )
  }

  environment {
    GIT_REPO = 'https://github.com/1MRCV/studentmanagemnt.git'

    DEV_ARTIFACT_STORAGE  = 'C:\\jenkins-artifacts\\DEV'
    PROD_ARTIFACT_STORAGE = 'C:\\jenkins-artifacts\\PROD'

    DEV_IIS_PATH  = 'C:\\inetpub\\dev'
    PROD_IIS_PATH = 'C:\\inetpub\\prod'

    DEV_APP_POOL  = 'student-dev-pool'
    PROD_APP_POOL = 'student-prod-pool'

    DEV_SITE  = 'student-dev'
    PROD_SITE = 'student-prod'

    DEV_PORT  = '8081'
    PROD_PORT = '8082'

    PUBLISH_FOLDER = 'publish'
  }

  stages {

    stage('Set Build Name') {
      steps {
        script {
          def date = new Date().format('yyyy-MM-dd')
          currentBuild.displayName = "#${env.BUILD_NUMBER} | ${params.ENVIRONMENT} | ${date}"
        }
      }
    }

    // ✅ DEV FLOW (Build + Deploy)
    stage('Checkout Code') {
      when {
        expression { params.ENVIRONMENT == 'DEV' }
      }
      steps {
        deleteDir()
        checkout([
          $class: 'GitSCM',
          branches: [[name: "${params.BRANCH}"]],
          userRemoteConfigs: [[url: "${env.GIT_REPO}"]],
          gitTool: 'Git-Windows'
        ])
      }
    }

    stage('Build + Publish') {
      when {
        expression { params.ENVIRONMENT == 'DEV' }
      }
      steps {
        powershell '''
          dotnet restore
          dotnet build --configuration Release
          dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish
        '''
      }
    }

    stage('Create Artifact') {
      when {
        expression { params.ENVIRONMENT == 'DEV' }
      }
      steps {
        powershell '''
          $build = $env:BUILD_NUMBER
          $date  = Get-Date -Format "yyyy-MM-dd"
          $storage = $env:DEV_ARTIFACT_STORAGE

          if (!(Test-Path $storage)) {
            New-Item -ItemType Directory -Path $storage | Out-Null
          }

          $folder = "$storage\\build_${build}_$date"
          New-Item -ItemType Directory -Path $folder -Force | Out-Null

          Compress-Archive `
            -Path "$env:PUBLISH_FOLDER\\*" `
            -DestinationPath "$folder\\artifact.zip" `
            -Force

          Write-Host "Artifact created: $folder"
        '''
      }
    }

  stage('Deploy to IIS') {
    steps {
        powershell '''
        if ("${params.ENVIRONMENT}" -eq "DEV") {
            $buildNum = "${env.BUILD_NUMBER}"
            $storage  = $env:DEV_ARTIFACT_STORAGE
        } else {
            $selected = "${params.ARTIFACT_BUILD}"
            $buildNum = $selected.Split("|")[0].Replace("Build_","").Trim()
            $storage  = $env:PROD_ARTIFACT_STORAGE
        }

        Write-Host "Build Number: $buildNum"
        Write-Host "Storage: $storage"

        $folder = Get-ChildItem $storage -Directory |
                  Where-Object { $_.Name -like "build_${buildNum}_*" } |
                  Select-Object -First 1

        if ($null -eq $folder) {
            throw "Artifact not found for build $buildNum"
        }

        Write-Host "Deploying from $($folder.FullName)"

        Remove-Item "$env:IIS_PATH\\*" -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item -Recurse "$($folder.FullName)\\*" "$env:IIS_PATH"

        Write-Host "Deployment completed"
        '''
    }
}
    stage('Verify') {
      steps {
        powershell '''
          Import-Module WebAdministration

          $appPool = if ("${params.ENVIRONMENT}" -eq "DEV") { $env:DEV_APP_POOL } else { $env:PROD_APP_POOL }

          $state = (Get-WebAppPoolState -Name $appPool).Value
          Write-Host "App Pool State: $state"

          if ($state -ne "Started") {
              throw "Deployment failed"
          }
        '''
      }
    }
  }

  post {
    success {
      echo "SUCCESS"
    }
    failure {
      echo "FAILED"
    }
  }
}
